using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NursingBackend.Services.Billing;
using NursingBackend.Services.Care;
using NursingBackend.Services.Visit;
using RabbitMQ.Client;

namespace NursingBackend.EventWorker;

public sealed class QueueBacklogReporterWorker(
	IServiceScopeFactory scopeFactory,
	IOptions<RabbitMqOptions> rabbitOptions,
	WorkerMetrics metrics,
	ILogger<QueueBacklogReporterWorker> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var options = rabbitOptions.Value;
		var factory = new ConnectionFactory
		{
			HostName = options.Host,
			Port = options.Port,
			UserName = options.UserName,
			Password = options.Password,
			DispatchConsumersAsync = true,
		};

		using var connection = factory.CreateConnection();
		using var channel = connection.CreateModel();
		RabbitMqTopology.Configure(channel, options);

		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, options.MetricsIntervalSeconds)));
		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				using var scope = scopeFactory.CreateScope();
				var billingDbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
				var careDbContext = scope.ServiceProvider.GetRequiredService<CareDbContext>();
				var visitDbContext = scope.ServiceProvider.GetRequiredService<VisitDbContext>();

				var billingBacklog = await billingDbContext.OutboxMessages.CountAsync(item => item.DispatchedAtUtc == null, stoppingToken);
				var careBacklog = await careDbContext.OutboxMessages.CountAsync(item => item.DispatchedAtUtc == null, stoppingToken);
				var visitBacklog = await visitDbContext.OutboxMessages.CountAsync(item => item.DispatchedAtUtc == null, stoppingToken);
				var mainDepth = channel.QueueDeclarePassive(options.Queue).MessageCount;
				var retryDepth = channel.QueueDeclarePassive(options.RetryQueue).MessageCount;
				var deadDepth = channel.QueueDeclarePassive(options.DeadLetterQueue).MessageCount;

				metrics.UpdateBacklogs(careBacklog, visitBacklog, billingBacklog);
				metrics.UpdateQueueDepths((long)mainDepth, (long)retryDepth, (long)deadDepth);

				logger.LogInformation(
					"Worker backlog metrics updated. billingOutbox={BillingBacklog}, careOutbox={CareBacklog}, visitOutbox={VisitBacklog}, mainQueue={MainDepth}, retryQueue={RetryDepth}, deadLetterQueue={DeadDepth}",
					billingBacklog,
					careBacklog,
					visitBacklog,
					mainDepth,
					retryDepth,
					deadDepth);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				logger.LogError(exception, "Failed to update worker backlog metrics.");
			}
		}
	}
}