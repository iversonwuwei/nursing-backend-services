using System.Diagnostics.Metrics;
using System.Threading;

namespace NursingBackend.EventWorker;

public sealed class WorkerMetrics
{
	private readonly Meter meter = new("NursingBackend.EventWorker", "1.0.0");
	private readonly Counter<long> publishedCounter;
	private readonly Counter<long> consumedCounter;
	private readonly Counter<long> retriedCounter;
	private readonly Counter<long> deadLetteredCounter;
	private readonly Counter<long> failuresCounter;

	private long careOutboxBacklog;
	private long visitOutboxBacklog;
	private long billingOutboxBacklog;
	private long mainQueueDepth;
	private long retryQueueDepth;
	private long deadLetterQueueDepth;

	public WorkerMetrics()
	{
		publishedCounter = meter.CreateCounter<long>("nursing.worker.events.published");
		consumedCounter = meter.CreateCounter<long>("nursing.worker.events.consumed");
		retriedCounter = meter.CreateCounter<long>("nursing.worker.events.retried");
		deadLetteredCounter = meter.CreateCounter<long>("nursing.worker.events.dead_lettered");
		failuresCounter = meter.CreateCounter<long>("nursing.worker.events.failures");

		meter.CreateObservableGauge("nursing.worker.backlog.care_outbox", () => Interlocked.Read(ref careOutboxBacklog));
		meter.CreateObservableGauge("nursing.worker.backlog.visit_outbox", () => Interlocked.Read(ref visitOutboxBacklog));
		meter.CreateObservableGauge("nursing.worker.backlog.billing_outbox", () => Interlocked.Read(ref billingOutboxBacklog));
		meter.CreateObservableGauge("nursing.worker.queue.main_depth", () => Interlocked.Read(ref mainQueueDepth));
		meter.CreateObservableGauge("nursing.worker.queue.retry_depth", () => Interlocked.Read(ref retryQueueDepth));
		meter.CreateObservableGauge("nursing.worker.queue.dead_letter_depth", () => Interlocked.Read(ref deadLetterQueueDepth));
	}

	public void RecordPublished(int count = 1) => publishedCounter.Add(count);
	public void RecordConsumed(int count = 1) => consumedCounter.Add(count);
	public void RecordRetried(int count = 1) => retriedCounter.Add(count);
	public void RecordDeadLettered(int count = 1) => deadLetteredCounter.Add(count);
	public void RecordFailure(int count = 1) => failuresCounter.Add(count);

	public void UpdateBacklogs(long care, long visit, long billing)
	{
		Interlocked.Exchange(ref careOutboxBacklog, care);
		Interlocked.Exchange(ref visitOutboxBacklog, visit);
		Interlocked.Exchange(ref billingOutboxBacklog, billing);
	}

	public void UpdateQueueDepths(long main, long retry, long deadLetter)
	{
		Interlocked.Exchange(ref mainQueueDepth, main);
		Interlocked.Exchange(ref retryQueueDepth, retry);
		Interlocked.Exchange(ref deadLetterQueueDepth, deadLetter);
	}
}