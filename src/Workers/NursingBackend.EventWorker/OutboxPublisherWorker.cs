using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NursingBackend.BuildingBlocks.Messaging;
using NursingBackend.Services.Billing;
using NursingBackend.Services.Care;
using NursingBackend.Services.Visit;
using RabbitMQ.Client;

namespace NursingBackend.EventWorker;

public sealed class OutboxPublisherWorker(
	IServiceScopeFactory scopeFactory,
	IOptions<RabbitMqOptions> rabbitOptions,
	WorkerMetrics metrics,
	ILogger<OutboxPublisherWorker> logger) : BackgroundService
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

		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.PublishIntervalSeconds)));
		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				await PublishPendingAsync(scopeFactory, channel, options, stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				logger.LogError(exception, "Failed while publishing outbox events to RabbitMQ.");
			}
		}
	}

	private async Task PublishPendingAsync(IServiceScopeFactory scopeFactory, IModel channel, RabbitMqOptions options, CancellationToken cancellationToken)
	{
		using var scope = scopeFactory.CreateScope();
		var billingDbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
		var careDbContext = scope.ServiceProvider.GetRequiredService<CareDbContext>();
		var visitDbContext = scope.ServiceProvider.GetRequiredService<VisitDbContext>();

		await PublishBillingAsync(billingDbContext, channel, options, cancellationToken);
		await PublishCareAsync(careDbContext, channel, options, cancellationToken);
		await PublishVisitAsync(visitDbContext, channel, options, cancellationToken);
	}

	private async Task PublishBillingAsync(BillingDbContext dbContext, IModel channel, RabbitMqOptions options, CancellationToken cancellationToken)
	{
		var pending = await dbContext.OutboxMessages
			.Where(item => item.DispatchedAtUtc == null && item.EventType == "InvoiceIssued")
			.OrderBy(item => item.CreatedAtUtc)
			.Take(options.BatchSize)
			.ToListAsync(cancellationToken);

		foreach (var message in pending)
		{
			Publish(channel, options.Exchange, BrokerTopology.InvoiceIssuedRoutingKey, new BrokerEventEnvelope(
				SourceService: "billing-service",
				TenantId: message.TenantId,
				AggregateType: message.AggregateType,
				AggregateId: message.AggregateId,
				EventType: message.EventType,
				PayloadJson: message.PayloadJson,
				CorrelationId: message.OutboxMessageId,
				OccurredAtUtc: message.CreatedAtUtc));

			message.DispatchedAtUtc = DateTimeOffset.UtcNow;
			metrics.RecordPublished();
		}

		if (pending.Count > 0)
		{
			await dbContext.SaveChangesAsync(cancellationToken);
		}
	}

	private async Task PublishCareAsync(CareDbContext dbContext, IModel channel, RabbitMqOptions options, CancellationToken cancellationToken)
	{
		var pending = await dbContext.OutboxMessages
			.Where(item => item.DispatchedAtUtc == null && item.EventType == "CarePlanGenerated")
			.OrderBy(item => item.CreatedAtUtc)
			.Take(options.BatchSize)
			.ToListAsync(cancellationToken);

		foreach (var message in pending)
		{
			Publish(channel, options.Exchange, BrokerTopology.CarePlanRoutingKey, new BrokerEventEnvelope(
				SourceService: "care-service",
				TenantId: message.TenantId,
				AggregateType: message.AggregateType,
				AggregateId: message.AggregateId,
				EventType: message.EventType,
				PayloadJson: message.PayloadJson,
				CorrelationId: message.OutboxMessageId,
				OccurredAtUtc: message.CreatedAtUtc));

			message.DispatchedAtUtc = DateTimeOffset.UtcNow;
			metrics.RecordPublished();
		}

		if (pending.Count > 0)
		{
			await dbContext.SaveChangesAsync(cancellationToken);
		}
	}

	private async Task PublishVisitAsync(VisitDbContext dbContext, IModel channel, RabbitMqOptions options, CancellationToken cancellationToken)
	{
		var pending = await dbContext.OutboxMessages
			.Where(item => item.DispatchedAtUtc == null && item.EventType == "VisitRequested")
			.OrderBy(item => item.CreatedAtUtc)
			.Take(options.BatchSize)
			.ToListAsync(cancellationToken);

		foreach (var message in pending)
		{
			Publish(channel, options.Exchange, BrokerTopology.VisitRequestedRoutingKey, new BrokerEventEnvelope(
				SourceService: "visit-service",
				TenantId: message.TenantId,
				AggregateType: message.AggregateType,
				AggregateId: message.AggregateId,
				EventType: message.EventType,
				PayloadJson: message.PayloadJson,
				CorrelationId: message.OutboxMessageId,
				OccurredAtUtc: message.CreatedAtUtc));

			message.DispatchedAtUtc = DateTimeOffset.UtcNow;
			metrics.RecordPublished();
		}

		if (pending.Count > 0)
		{
			await dbContext.SaveChangesAsync(cancellationToken);
		}
	}

	private static void Publish(IModel channel, string exchange, string routingKey, BrokerEventEnvelope envelope)
	{
		var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
		var properties = channel.CreateBasicProperties();
		properties.Persistent = true;
		channel.BasicPublish(exchange, routingKey, properties, body);
	}
}