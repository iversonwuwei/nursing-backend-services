namespace NursingBackend.BuildingBlocks.Entities;

public sealed class OutboxMessageEntity
{
    public string OutboxMessageId { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string AggregateType { get; set; } = default!;
    public string AggregateId { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DispatchedAtUtc { get; set; }
}