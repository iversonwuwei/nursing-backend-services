using NursingBackend.BuildingBlocks.Messaging;
using RabbitMQ.Client;

namespace NursingBackend.EventWorker;

public static class RabbitMqTopology
{
	public static void Configure(IModel channel, RabbitMqOptions options)
	{
		channel.ExchangeDeclare(options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
		channel.ExchangeDeclare(options.RetryExchange, ExchangeType.Topic, durable: true, autoDelete: false);
		channel.ExchangeDeclare(options.DeadLetterExchange, ExchangeType.Topic, durable: true, autoDelete: false);

		channel.QueueDeclare(options.Queue, durable: true, exclusive: false, autoDelete: false);
		channel.QueueBind(options.Queue, options.Exchange, BrokerTopology.CarePlanRoutingKey);
		channel.QueueBind(options.Queue, options.Exchange, BrokerTopology.VisitRequestedRoutingKey);
		channel.QueueBind(options.Queue, options.Exchange, BrokerTopology.InvoiceIssuedRoutingKey);

		channel.QueueDeclare(
			options.RetryQueue,
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: new Dictionary<string, object>
			{
				["x-message-ttl"] = options.RetryIntervalSeconds * 1000,
				["x-dead-letter-exchange"] = options.Exchange,
			});
		channel.QueueBind(options.RetryQueue, options.RetryExchange, BrokerTopology.CarePlanRoutingKey);
		channel.QueueBind(options.RetryQueue, options.RetryExchange, BrokerTopology.VisitRequestedRoutingKey);
		channel.QueueBind(options.RetryQueue, options.RetryExchange, BrokerTopology.InvoiceIssuedRoutingKey);

		channel.QueueDeclare(options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
		channel.QueueBind(options.DeadLetterQueue, options.DeadLetterExchange, BrokerTopology.CarePlanRoutingKey);
		channel.QueueBind(options.DeadLetterQueue, options.DeadLetterExchange, BrokerTopology.VisitRequestedRoutingKey);
		channel.QueueBind(options.DeadLetterQueue, options.DeadLetterExchange, BrokerTopology.InvoiceIssuedRoutingKey);
	}
}