namespace NursingBackend.BuildingBlocks.Entities;

public sealed class NotificationProviderCallbackReceiptEntity
{
	public string ReceiptId { get; set; } = default!;
	public string Provider { get; set; } = default!;
	public string DedupeKey { get; set; } = default!;
	public string? ProviderMessageId { get; set; }
	public string? NotificationId { get; set; }
	public string? CorrelationId { get; set; }
	public string Status { get; set; } = default!;
	public string SignatureStatus { get; set; } = default!;
	public DateTimeOffset ReceivedAtUtc { get; set; }
	public DateTimeOffset? ProcessedAtUtc { get; set; }
}