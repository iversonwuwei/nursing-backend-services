using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Messaging;
using NursingBackend.Services.Billing;
using NursingBackend.Services.Care;
using NursingBackend.Services.Notification;
using NursingBackend.Services.Visit;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NursingBackend.EventWorker;

public sealed class NotificationConsumerWorker(
	IServiceScopeFactory scopeFactory,
	IOptions<RabbitMqOptions> rabbitOptions,
	WorkerMetrics metrics,
	ILogger<NotificationConsumerWorker> logger) : BackgroundService
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

		var consumer = new AsyncEventingBasicConsumer(channel);
		consumer.Received += async (_, eventArgs) =>
		{
			try
			{
				var envelope = JsonSerializer.Deserialize<BrokerEventEnvelope>(Encoding.UTF8.GetString(eventArgs.Body.ToArray()));
				if (envelope is not null)
				{
					await PersistNotificationsAsync(scopeFactory, envelope, stoppingToken);
					metrics.RecordConsumed();
				}

				channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
			}
			catch (Exception exception)
			{
				logger.LogError(exception, "Failed to consume broker event.");
				metrics.RecordFailure();
				var retryCount = WorkerRetryPolicy.GetRetryCount(eventArgs.BasicProperties.Headers);
				var exchange = WorkerRetryPolicy.ShouldDeadLetter(retryCount, options.MaxRetryAttempts)
					? options.DeadLetterExchange
					: options.RetryExchange;
				var properties = WorkerRetryPolicy.CreateForwardProperties(channel, eventArgs.BasicProperties, retryCount + 1);
				channel.BasicPublish(exchange, eventArgs.RoutingKey, properties, eventArgs.Body);
				if (exchange == options.DeadLetterExchange)
				{
					metrics.RecordDeadLettered();
				}
				else
				{
					metrics.RecordRetried();
				}
				channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
			}
		};

		channel.BasicConsume(options.Queue, autoAck: false, consumer);
		await Task.Delay(Timeout.Infinite, stoppingToken);
	}

	private static async Task PersistNotificationsAsync(IServiceScopeFactory scopeFactory, BrokerEventEnvelope envelope, CancellationToken cancellationToken)
	{
		using var scope = scopeFactory.CreateScope();
		var notificationDbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
		var requests = BuildRequests(envelope);

		foreach (var request in requests)
		{
			var notificationId = BuildNotificationId(envelope, request);
			var exists = await notificationDbContext.Notifications.AnyAsync(item => item.NotificationId == notificationId, cancellationToken);
			if (exists)
			{
				continue;
			}

			notificationDbContext.Notifications.Add(new NotificationMessageEntity
			{
				NotificationId = notificationId,
				TenantId = envelope.TenantId,
				Audience = request.Audience,
				AudienceKey = request.AudienceKey,
				Category = request.Category,
				Title = request.Title,
				Body = request.Body,
				SourceService = request.SourceService,
				SourceEntityId = request.SourceEntityId,
				CorrelationId = request.CorrelationId,
				Status = "Queued",
				CreatedAtUtc = DateTimeOffset.UtcNow,
			});
		}

		await notificationDbContext.SaveChangesAsync(cancellationToken);
	}

	private static IReadOnlyList<NotificationDispatchRequest> BuildRequests(BrokerEventEnvelope envelope)
	{
		var outboxMessage = new OutboxMessageEntity
		{
			OutboxMessageId = envelope.CorrelationId,
			TenantId = envelope.TenantId,
			AggregateType = envelope.AggregateType,
			AggregateId = envelope.AggregateId,
			EventType = envelope.EventType,
			PayloadJson = envelope.PayloadJson,
			CreatedAtUtc = envelope.OccurredAtUtc,
		};

		return envelope.SourceService switch
		{
			"billing-service" => BillingOutboxNotificationDispatcher.BuildRequests(outboxMessage, envelope.CorrelationId),
			"care-service" => CareOutboxNotificationDispatcher.BuildRequests(outboxMessage, envelope.CorrelationId),
			"visit-service" => VisitOutboxNotificationDispatcher.BuildRequests(outboxMessage, envelope.CorrelationId),
			_ => Array.Empty<NotificationDispatchRequest>(),
		};
	}

	private static string BuildNotificationId(BrokerEventEnvelope envelope, NotificationDispatchRequest request)
	{
		return $"NTF-{envelope.SourceService}-{request.Audience}-{request.AudienceKey}-{request.SourceEntityId}";
	}
}