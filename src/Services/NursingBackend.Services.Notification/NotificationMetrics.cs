using System.Diagnostics.Metrics;

namespace NursingBackend.Services.Notification;

public sealed class NotificationMetrics
{
	private readonly Meter meter = new("NursingBackend.Services.Notification", "1.0.0");
	private readonly Counter<long> queuedCounter;
	private readonly Counter<long> deliveredCounter;
	private readonly Counter<long> failedCounter;
	private readonly Counter<long> providerCallbacksCounter;
	private readonly Counter<long> compensationRequestedCounter;
	private readonly Counter<long> compensationRequestFailedCounter;

	public NotificationMetrics()
	{
		queuedCounter = meter.CreateCounter<long>("nursing_notification_delivery_queued");
		deliveredCounter = meter.CreateCounter<long>("nursing_notification_delivery_delivered");
		failedCounter = meter.CreateCounter<long>("nursing_notification_delivery_failed");
		providerCallbacksCounter = meter.CreateCounter<long>("nursing_notification_provider_callbacks_received");
		compensationRequestedCounter = meter.CreateCounter<long>("nursing_notification_compensation_requested");
		compensationRequestFailedCounter = meter.CreateCounter<long>("nursing_notification_compensation_request_failed");
	}

	public void RecordQueued() => queuedCounter.Add(1);
	public void RecordProviderCallback() => providerCallbacksCounter.Add(1);
	public void RecordCompensationRequested() => compensationRequestedCounter.Add(1);
	public void RecordCompensationRequestFailed() => compensationRequestFailedCounter.Add(1);

	public void RecordDeliveryOutcome(string status)
	{
		if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
		{
			failedCounter.Add(1);
			return;
		}

		if (string.Equals(status, "Delivered", StringComparison.OrdinalIgnoreCase))
		{
			deliveredCounter.Add(1);
		}
	}
}