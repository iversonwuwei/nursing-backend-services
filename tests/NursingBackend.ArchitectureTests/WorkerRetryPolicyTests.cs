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
}