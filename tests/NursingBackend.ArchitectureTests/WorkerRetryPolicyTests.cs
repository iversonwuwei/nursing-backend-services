using NursingBackend.EventWorker;

namespace NursingBackend.ArchitectureTests;

public class WorkerRetryPolicyTests
{
	[Fact]
	public void Retry_count_defaults_to_zero_when_headers_missing()
	{
		Assert.Equal(0, WorkerRetryPolicy.GetRetryCount(null));
	}

	[Fact]
	public void Retry_count_reads_utf8_header_value()
	{
		var headers = new Dictionary<string, object>
		{
			["x-retry-count"] = System.Text.Encoding.UTF8.GetBytes("2"),
		};

		Assert.Equal(2, WorkerRetryPolicy.GetRetryCount(headers));
	}

	[Fact]
	public void Retry_policy_dead_letters_when_max_attempts_reached()
	{
		Assert.True(WorkerRetryPolicy.ShouldDeadLetter(3, 3));
		Assert.False(WorkerRetryPolicy.ShouldDeadLetter(2, 3));
	}

	[Fact]
	public void Replay_headers_remove_retry_header()
	{
		var sourceHeaders = new Dictionary<string, object>
		{
			["x-retry-count"] = "3",
			["x-origin"] = "dead-letter",
		};

		var replayHeaders = WorkerRetryPolicy.CreateReplayHeaders(sourceHeaders);

		Assert.False(replayHeaders.ContainsKey("x-retry-count"));
		Assert.Equal("dead-letter", replayHeaders["x-origin"]);
	}

	[Fact]
	public void Rabbitmq_prefetch_count_defaults_to_bounded_window()
	{
		var options = new RabbitMqOptions();

		Assert.Equal((ushort)20, options.ResolveConsumerPrefetchCount());
	}

	[Fact]
	public void Rabbitmq_prefetch_count_falls_back_to_batch_size_when_disabled()
	{
		var options = new RabbitMqOptions
		{
			ConsumerPrefetchCount = 0,
			BatchSize = 8,
		};

		Assert.Equal((ushort)8, options.ResolveConsumerPrefetchCount());
	}

	[Fact]
	public void Rabbitmq_prefetch_count_is_clamped_to_amqp_limit()
	{
		var options = new RabbitMqOptions
		{
			ConsumerPrefetchCount = ushort.MaxValue + 1,
		};

		Assert.Equal(ushort.MaxValue, options.ResolveConsumerPrefetchCount());
	}
}
