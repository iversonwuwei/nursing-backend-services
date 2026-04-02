namespace NursingBackend.BuildingBlocks.Entities;

public sealed class BillingInvoiceEntity
{
	public string InvoiceId { get; set; } = default!;
	public string TenantId { get; set; } = default!;
	public string ElderId { get; set; } = default!;
	public string ElderName { get; set; } = default!;
	public string PackageName { get; set; } = default!;
	public decimal Amount { get; set; }
	public DateTimeOffset DueAtUtc { get; set; }
	public string Status { get; set; } = default!;
	public string NotificationStatus { get; set; } = default!;
	public string? LastNotificationFailureCode { get; set; }
	public string? LastNotificationFailureReason { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? UpdatedAtUtc { get; set; }
}

public sealed class BillingCompensationRecordEntity
{
	public string CompensationId { get; set; } = default!;
	public string TenantId { get; set; } = default!;
	public string InvoiceId { get; set; } = default!;
	public string NotificationId { get; set; } = default!;
	public string CorrelationId { get; set; } = default!;
	public string FailureCode { get; set; } = default!;
	public string FailureReason { get; set; } = default!;
	public string Status { get; set; } = default!;
	public string? ResolutionNote { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ResolvedAtUtc { get; set; }
}