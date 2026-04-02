namespace NursingBackend.BuildingBlocks.Entities;

public sealed class NotificationMessageEntity
{
    public string NotificationId { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string AudienceKey { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string SourceService { get; set; } = default!;
    public string SourceEntityId { get; set; } = default!;
    public string CorrelationId { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}