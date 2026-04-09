namespace NursingBackend.BuildingBlocks.Entities;

public sealed class StaticTextEntity
{
    public required string StaticTextId { get; init; }
    public required string TenantId { get; init; }
    public required string Namespace { get; init; }
    public required string TextKey { get; init; }
    public required string Locale { get; init; }
    public required string TextValue { get; set; }
    public string? Description { get; set; }
    public required int Version { get; set; }
    public string? UpdatedBy { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class OptionGroupEntity
{
    public required string OptionGroupId { get; init; }
    public required string TenantId { get; init; }
    public required string GroupCode { get; init; }
    public required string GroupName { get; set; }
    public string? Description { get; set; }
    public required bool IsSystem { get; init; }
    public required string Status { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class OptionItemEntity
{
    public required string OptionItemId { get; init; }
    public required string GroupId { get; init; }
    public required string OptionCode { get; init; }
    public required string LabelZh { get; set; }
    public string? LabelEn { get; set; }
    public required int SortOrder { get; set; }
    public required bool IsActive { get; set; }
    public required bool IsDefault { get; set; }
    public string? ExtraDataJson { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ContentAuditLogEntity
{
    public required string AuditLogId { get; init; }
    public required string TenantId { get; init; }
    public required string OperatorId { get; init; }
    public required string OperatorName { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceId { get; init; }
    public required string Action { get; init; }
    public string? BeforeSnapshotJson { get; init; }
    public string? AfterSnapshotJson { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class AppConfigSnapshotEntity
{
    public required string SnapshotId { get; init; }
    public required string TenantId { get; init; }
    public required string Namespace { get; init; }
    public required string Locale { get; init; }
    public required long SnapshotVersion { get; init; }
    public required string ContentJson { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
}
