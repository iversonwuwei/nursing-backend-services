using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.Services.Billing;
using NursingBackend.Services.Care;
using NursingBackend.Services.Visit;

namespace NursingBackend.ArchitectureTests;

public class OutboxProjectionTests
{
	[Fact]
	public void Care_outbox_event_projects_to_nani_notification()
	{
		var message = new OutboxMessageEntity
		{
			OutboxMessageId = "OUT-CARE-1",
			TenantId = "tenant-demo",
			AggregateType = "CarePlan",
			AggregateId = "CP-1",
			EventType = "CarePlanGenerated",
			PayloadJson = "{\"ElderId\":\"ELD-1\",\"ElderName\":\"王秀兰\",\"CareLevel\":\"全护理\",\"TaskCount\":3}",
			CreatedAtUtc = DateTimeOffset.UtcNow,
		};

		var requests = CareOutboxNotificationDispatcher.BuildRequests(message, "corr-1");

		var request = Assert.Single(requests);
		Assert.Equal("nani", request.Audience);
		Assert.Equal("ELD-1", request.AudienceKey);
		Assert.Equal("care-service", request.SourceService);
		Assert.Contains("3 项护理任务", request.Body);
	}

	[Fact]
	public void Visit_outbox_event_projects_to_family_notification()
	{
		var message = new OutboxMessageEntity
		{
			OutboxMessageId = "OUT-VISIT-1",
			TenantId = "tenant-demo",
			AggregateType = "Visit",
			AggregateId = "VIS-1",
			EventType = "VisitRequested",
			PayloadJson = "{\"VisitId\":\"VIS-1\",\"ElderId\":\"ELD-9\",\"VisitorName\":\"李明\",\"PlannedAtUtc\":\"2026-04-02T10:30:00+00:00\"}",
			CreatedAtUtc = DateTimeOffset.UtcNow,
		};

		var requests = VisitOutboxNotificationDispatcher.BuildRequests(message, "corr-2");

		var request = Assert.Single(requests);
		Assert.Equal("family", request.Audience);
		Assert.Equal("ELD-9", request.AudienceKey);
		Assert.Equal("visit-service", request.SourceService);
		Assert.Contains("探视申请已提交", request.Title);
	}

	[Fact]
	public void Billing_outbox_event_projects_to_family_notification()
	{
		var message = new OutboxMessageEntity
		{
			OutboxMessageId = "OUT-BILL-1",
			TenantId = "tenant-demo",
			AggregateType = "BillingInvoice",
			AggregateId = "INV-1",
			EventType = "InvoiceIssued",
			PayloadJson = "{\"InvoiceId\":\"INV-1\",\"ElderId\":\"ELD-3\",\"ElderName\":\"陈玉芳\",\"Amount\":3999.50,\"DueAtUtc\":\"2026-04-05T09:00:00+00:00\"}",
			CreatedAtUtc = DateTimeOffset.UtcNow,
		};

		var requests = BillingOutboxNotificationDispatcher.BuildRequests(message, "corr-3");

		var request = Assert.Single(requests);
		Assert.Equal("family", request.Audience);
		Assert.Equal("ELD-3", request.AudienceKey);
		Assert.Equal("billing-service", request.SourceService);
		Assert.Contains("3999.50", request.Body);
	}
}