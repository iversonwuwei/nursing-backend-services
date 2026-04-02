using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.Services.Notification;

namespace NursingBackend.ArchitectureTests;

public class BillingNotificationCompensationPolicyTests
{
	[Fact]
	public void Billing_failure_should_request_compensation()
	{
		var entity = new NotificationMessageEntity
		{
			NotificationId = "NTF-1",
			TenantId = "tenant-demo",
			Audience = "family",
			AudienceKey = "ELD-1",
			Category = "billing-invoice",
			Title = "账单通知",
			Body = "body",
			SourceService = "billing-service",
			SourceEntityId = "INV-1",
			CorrelationId = "corr-1",
			Status = "Queued",
			CreatedAtUtc = DateTimeOffset.UtcNow,
		};

		Assert.True(BillingNotificationCompensationPolicy.ShouldRequest(entity, "Failed"));
	}

	[Fact]
	public void Compensation_request_uses_default_failure_values_when_missing()
	{
		var entity = new NotificationMessageEntity
		{
			NotificationId = "NTF-1",
			TenantId = "tenant-demo",
			Audience = "family",
			AudienceKey = "ELD-1",
			Category = "billing-invoice",
			Title = "账单通知",
			Body = "body",
			SourceService = "billing-service",
			SourceEntityId = "INV-1",
			CorrelationId = "corr-1",
			Status = "Queued",
			CreatedAtUtc = DateTimeOffset.UtcNow,
		};

		var request = BillingNotificationCompensationPolicy.BuildRequest(entity, new NotificationDeliveryResultRequest(
			Status: "Failed",
			Channel: "sms",
			FailureCode: null,
			FailureReason: null));

		Assert.Equal("NTF-1", request.NotificationId);
		Assert.Equal("corr-1", request.CorrelationId);
		Assert.Equal("delivery-failed", request.FailureCode);
	}
}