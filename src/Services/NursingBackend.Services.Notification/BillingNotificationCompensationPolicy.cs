using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Notification;

public static class BillingNotificationCompensationPolicy
{
	public static bool ShouldRequest(NotificationMessageEntity entity, string status)
	{
		return string.Equals(entity.SourceService, "billing-service", StringComparison.OrdinalIgnoreCase)
			&& string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase);
	}

	public static BillingNotificationCompensationRequest BuildRequest(NotificationMessageEntity entity, NotificationDeliveryResultRequest request)
	{
		return new BillingNotificationCompensationRequest(
			NotificationId: entity.NotificationId,
			CorrelationId: entity.CorrelationId,
			FailureCode: string.IsNullOrWhiteSpace(request.FailureCode) ? "delivery-failed" : request.FailureCode,
			FailureReason: string.IsNullOrWhiteSpace(request.FailureReason) ? "Notification delivery failed." : request.FailureReason);
	}
}