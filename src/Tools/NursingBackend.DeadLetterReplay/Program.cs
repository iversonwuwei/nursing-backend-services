using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NursingBackend.EventWorker;
using RabbitMQ.Client;

var command = ReplayCommandOptions.Parse(args);
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLogging(logging => logging.AddSimpleConsole());

var rabbitOptions = new RabbitMqOptions();
builder.Configuration.GetSection("RabbitMq").Bind(rabbitOptions);

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DeadLetterReplay");

logger.LogInformation(
	"Starting dead-letter replay in mode {Mode} with limit {Limit}.",
	command.Execute ? (command.KeepSource ? "execute-keep-source" : "execute-delete-source") : "dry-run",
	command.Limit);

var factory = new ConnectionFactory
{
	HostName = rabbitOptions.Host,
	Port = rabbitOptions.Port,
	UserName = rabbitOptions.UserName,
	Password = rabbitOptions.Password,
	DispatchConsumersAsync = false,
};

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();
RabbitMqTopology.Configure(channel, rabbitOptions);

var inspected = 0;
var replayed = 0;

while (inspected < command.Limit)
{
	var result = channel.BasicGet(rabbitOptions.DeadLetterQueue, autoAck: false);
	if (result is null)
	{
		break;
	}

	inspected++;
	var retryCount = WorkerRetryPolicy.GetRetryCount(result.BasicProperties.Headers);
	logger.LogInformation(
		"DLQ message {Index}: routingKey={RoutingKey}, correlationId={CorrelationId}, retryCount={RetryCount}",
		inspected,
		result.RoutingKey,
		result.BasicProperties.CorrelationId ?? "n/a",
		retryCount);

	if (!command.Execute)
	{
		continue;
	}

	var replayProperties = WorkerRetryPolicy.CreateReplayProperties(channel, result.BasicProperties);
	channel.BasicPublish(rabbitOptions.Exchange, result.RoutingKey, replayProperties, result.Body);
	logger.LogInformation(
		"Replayed message {Index} to exchange {Exchange} with routingKey {RoutingKey}.",
		inspected,
		rabbitOptions.Exchange,
		result.RoutingKey);
		replayed++;

	if (!command.KeepSource)
	{
		channel.BasicAck(result.DeliveryTag, multiple: false);
	}
	else
	{
		logger.LogWarning(
			"Keeping original dead-letter message for correlationId {CorrelationId}; it will be re-queued when the tool exits.",
			result.BasicProperties.CorrelationId ?? "n/a");
	}
}

logger.LogInformation(
	"Dead-letter replay finished. inspected={Inspected}, replayed={Replayed}, mode={Mode}",
	inspected,
	replayed,
	command.Execute ? (command.KeepSource ? "execute-keep-source" : "execute-delete-source") : "dry-run");

internal sealed record ReplayCommandOptions(int Limit, bool Execute, bool KeepSource)
{
	public static ReplayCommandOptions Parse(string[] args)
	{
		var limit = 20;
		var execute = false;
		var keepSource = false;

		for (var index = 0; index < args.Length; index++)
		{
			var arg = args[index];
			switch (arg)
			{
				case "--dry-run":
					execute = false;
					break;
				case "--execute":
					execute = true;
					break;
				case "--keep-source":
					keepSource = true;
					break;
				case "--limit" when index + 1 < args.Length:
					if (!int.TryParse(args[++index], out limit) || limit <= 0)
					{
						throw new ArgumentException("--limit must be a positive integer.");
					}
					break;
				default:
					if (arg.StartsWith("--limit=", StringComparison.Ordinal))
					{
						var value = arg[8..];
						if (!int.TryParse(value, out limit) || limit <= 0)
						{
							throw new ArgumentException("--limit must be a positive integer.");
						}
						break;
					}

					throw new ArgumentException($"Unsupported argument: {arg}");
			}
		}

		if (keepSource && !execute)
		{
			throw new ArgumentException("--keep-source requires --execute.");
		}

		return new ReplayCommandOptions(limit, execute, keepSource);
	}
}