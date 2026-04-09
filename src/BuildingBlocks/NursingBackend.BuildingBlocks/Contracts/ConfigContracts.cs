namespace NursingBackend.BuildingBlocks.Contracts;

// ── Static Text ──────────────────────────────────────────────

public sealed record StaticTextCreateRequest(
    string Namespace,
    string TextKey,
    string Locale,
    string TextValue,
    string? Description);

public sealed record StaticTextUpdateRequest(
    string TextValue,
    string? Description,
    int Version);

public sealed record StaticTextResponse(
    string Id,
    string TenantId,
    string Namespace,
    string TextKey,
    string Locale,
    string TextValue,
    string? Description,
    int Version,
    string? UpdatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record StaticTextListResponse(
    IReadOnlyList<StaticTextResponse> Items,
    int Total,
    int Page,
    int PageSize);

// ── Option Group ─────────────────────────────────────────────

public sealed record OptionGroupCreateRequest(
    string GroupCode,
    string GroupName,
    string? Description);

public sealed record OptionGroupUpdateRequest(
    string GroupName,
    string? Description);

public sealed record OptionGroupResponse(
    string Id,
    string TenantId,
    string GroupCode,
    string GroupName,
    string? Description,
    bool IsSystem,
    string Status,
    int ItemCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record OptionGroupListResponse(
    IReadOnlyList<OptionGroupResponse> Items);

// ── Option Item ──────────────────────────────────────────────

public sealed record OptionItemCreateRequest(
    string OptionCode,
    string LabelZh,
    string? LabelEn,
    int SortOrder,
    bool IsDefault);

public sealed record OptionItemUpdateRequest(
    string LabelZh,
    string? LabelEn,
    int SortOrder,
    bool IsDefault);

public sealed record OptionItemResponse(
    string Id,
    string GroupId,
    string OptionCode,
    string LabelZh,
    string? LabelEn,
    int SortOrder,
    bool IsActive,
    bool IsDefault,
    DateTimeOffset UpdatedAtUtc);

public sealed record OptionItemReorderRequest(
    IReadOnlyList<OptionItemOrderEntry> Ordering);

public sealed record OptionItemOrderEntry(
    string ItemId,
    int SortOrder);

// ── Audit Log ────────────────────────────────────────────────

public sealed record ContentAuditLogResponse(
    string Id,
    string TenantId,
    string OperatorId,
    string OperatorName,
    string ResourceType,
    string ResourceId,
    string Action,
    string? BeforeSnapshot,
    string? AfterSnapshot,
    DateTimeOffset CreatedAtUtc);

public sealed record ContentAuditLogListResponse(
    IReadOnlyList<ContentAuditLogResponse> Items,
    int Total,
    int Page,
    int PageSize);

// ── App Config ───────────────────────────────────────────────

public sealed record AppConfigSnapshotResponse(
    long SnapshotVersion,
    Dictionary<string, string> Texts,
    Dictionary<string, IReadOnlyList<AppConfigOptionResponse>> Options);

public sealed record AppConfigOptionResponse(
    string Code,
    string Label,
    int SortOrder);

public sealed record AppConfigDeltaResponse(
    long CurrentVersion,
    Dictionary<string, string> ChangedTexts,
    Dictionary<string, IReadOnlyList<AppConfigOptionResponse>> ChangedOptions);
