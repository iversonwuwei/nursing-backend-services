namespace NursingBackend.EventWorker;

public sealed class RabbitMqOptions
{
	public string Host { get; init; } = "localhost";
	public int Port { get; init; } = 5672;
	public string UserName { get; init; } = "nursing";
	public string Password { get; init; } = "nursing";
	public string Exchange { get; init; } = "nursing.domain.events";
	public string Queue { get; init; } = "nursing.notification.events";
	public string RetryExchange { get; init; } = "nursing.domain.events.retry";
	public string RetryQueue { get; init; } = "nursing.notification.events.retry";
	public string DeadLetterExchange { get; init; } = "nursing.domain.events.dead";
	public string DeadLetterQueue { get; init; } = "nursing.notification.events.dead";
	public int RetryIntervalSeconds { get; init; } = 15;
	public int MetricsIntervalSeconds { get; init; } = 15;
	public int MaxRetryAttempts { get; init; } = 3;
	public int PublishIntervalSeconds { get; init; } = 5;
	public int BatchSize { get; init; } = 20;
}