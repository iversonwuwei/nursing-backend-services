using RabbitMQ.Client;

namespace NursingBackend.EventWorker;

public static class WorkerRetryPolicy
{
	private const string RetryHeaderName = "x-retry-count";

	public static int GetRetryCount(IDictionary<string, object>? headers)
	{
		if (headers is null || !headers.TryGetValue(RetryHeaderName, out var value) || value is null)
		{
			return 0;
		}

		return value switch
		{
			byte[] bytes when int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
			int number => number,
			long number => (int)number,
			_ => 0,
		};
	}

	public static bool ShouldDeadLetter(int currentRetryCount, int maxRetryAttempts)
	{
		return currentRetryCount >= maxRetryAttempts;
	}

	public static IBasicProperties CreateForwardProperties(IModel channel, IBasicProperties source, int retryCount)
	{
		var properties = channel.CreateBasicProperties();
		properties.Persistent = true;
		properties.ContentType = source.ContentType;
		properties.CorrelationId = source.CorrelationId;
		properties.Type = source.Type;
		properties.Headers = new Dictionary<string, object>(source.Headers ?? new Dictionary<string, object>())
		{
			[RetryHeaderName] = retryCount.ToString(),
		};
		return properties;
	}

	public static IBasicProperties CreateReplayProperties(IModel channel, IBasicProperties source)
	{
		var properties = channel.CreateBasicProperties();
		properties.Persistent = true;
		properties.ContentType = source.ContentType;
		properties.CorrelationId = source.CorrelationId;
		properties.Type = source.Type;
		properties.Headers = CreateReplayHeaders(source.Headers);
		return properties;
	}

	public static IDictionary<string, object> CreateReplayHeaders(IDictionary<string, object>? sourceHeaders)
	{
		var headers = new Dictionary<string, object>(sourceHeaders ?? new Dictionary<string, object>());
		headers.Remove(RetryHeaderName);
		return headers;
	}
}