using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NursingBackend.Services.Notification;

public sealed class NotificationTelemetry
{
	private static readonly Meter Meter = new("NursingBackend.Services.Notification", "1.0.0");
	private static readonly ActivitySource ActivitySource = new("NursingBackend.Services.Notification");
	private readonly Counter<long> dispatchCounter = Meter.CreateCounter<long>("nursing_notification_dispatched_total");
	private readonly Counter<long> deliveredCounter = Meter.CreateCounter<long>("nursing_notification_delivery_delivered_total");
	private readonly Counter<long> failedCounter = Meter.CreateCounter<long>("nursing_notification_delivery_failed_total");
	private readonly Counter<long> compensationRequestedCounter = Meter.CreateCounter<long>("nursing_notification_compensation_requested_total");
	private readonly Counter<long> compensationRequestFailedCounter = Meter.CreateCounter<long>("nursing_notification_compensation_request_failed_total");
	private readonly Counter<long> providerCallbackCounter = Meter.CreateCounter<long>("nursing_notification_provider_callbacks_total");
	private readonly Counter<long> providerCallbackDuplicateCounter = Meter.CreateCounter<long>("nursing_notification_provider_callback_duplicates_total");
	private readonly Counter<long> providerSignatureFailureCounter = Meter.CreateCounter<long>("nursing_notification_provider_signature_failures_total");

	public Activity? StartActivity(string name, params KeyValuePair<string, object?>[] tags)
	{
		var activity = ActivitySource.StartActivity(name, ActivityKind.Internal);
		if (activity is null)
		{
			return null;
		}

		foreach (var tag in tags)
		{
			activity.SetTag(tag.Key, tag.Value);
		}

		return activity;
	}

	public void RecordDispatch(string sourceService, string category)
	{
		dispatchCounter.Add(1,
			new KeyValuePair<string, object?>("notification.source_service", sourceService),
			new KeyValuePair<string, object?>("notification.category", category));
	}

	public void RecordDelivery(string status, string channel, string sourceService)
	{
		var counter = string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
			? failedCounter
			: deliveredCounter;

		counter.Add(1,
			new KeyValuePair<string, object?>("notification.status", status),
			new KeyValuePair<string, object?>("notification.channel", channel),
			new KeyValuePair<string, object?>("notification.source_service", sourceService));
	}

	public void RecordCompensationRequest(bool succeeded, string sourceService)
	{
		var counter = succeeded ? compensationRequestedCounter : compensationRequestFailedCounter;
		counter.Add(1, new KeyValuePair<string, object?>("notification.source_service", sourceService));
	}

	public void RecordProviderCallback(string provider, string status)
	{
		providerCallbackCounter.Add(1,
			new KeyValuePair<string, object?>("provider.name", provider),
			new KeyValuePair<string, object?>("notification.status", status));
	}

	public void RecordProviderDuplicate(string provider)
	{
		providerCallbackDuplicateCounter.Add(1,
			new KeyValuePair<string, object?>("provider.name", provider));
	}

	public void RecordProviderSignatureFailure(string provider)
	{
		providerSignatureFailureCounter.Add(1,
			new KeyValuePair<string, object?>("provider.name", provider));
	}
}