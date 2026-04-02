namespace NursingBackend.BuildingBlocks.Entities;

public sealed class NotificationDeliveryAttemptEntity
{
	public string DeliveryAttemptId { get; set; } = default!;
	public string NotificationId { get; set; } = default!;
	public string TenantId { get; set; } = default!;
	public string SourceService { get; set; } = default!;
	public string SourceEntityId { get; set; } = default!;
	public string CorrelationId { get; set; } = default!;
	public string Channel { get; set; } = default!;
	public string Status { get; set; } = default!;
	public string? FailureCode { get; set; }
	public string? FailureReason { get; set; }
	public string CompensationStatus { get; set; } = default!;
	public string? CompensationReferenceId { get; set; }
	public DateTimeOffset AttemptedAtUtc { get; set; }
}