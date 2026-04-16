namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record AdmissionCreateRequest(
    string AdmissionReference,
    string ElderName,
    int Age,
    string Gender,
    string CareLevel,
    string RoomNumber,
    string FamilyContactName,
    string FamilyContactPhone,
    IReadOnlyList<string> MedicalAlerts,
    string? IdentityCard = null,
    string? BirthDate = null,
    string? ElderPhone = null,
    int? AdlScore = null,
    string? CognitiveLevel = null,
    string? EntrustmentType = null,
    string? EntrustmentOrganization = null,
    decimal? MonthlySubsidy = null,
    IReadOnlyList<string>? ServiceItems = null,
    string? ServiceNotes = null);

public sealed record AdmissionRecordResponse(
    string AdmissionId,
    string ElderId,
    string TenantId,
    string ElderName,
    string CareLevel,
    string RoomNumber,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record ElderProfileSummaryResponse(
    string ElderId,
    string TenantId,
    string ElderName,
    int Age,
    string Gender,
    string CareLevel,
    string RoomNumber,
    string AdmissionStatus,
    string? IdentityCard,
    string? BirthDate,
    string? ElderPhone,
    string FamilyContactName,
    string FamilyContactPhone,
    int? AdlScore,
    string? CognitiveLevel,
    IReadOnlyList<string> MedicalAlerts,
    string? EntrustmentType,
    string? EntrustmentOrganization,
    decimal? MonthlySubsidy,
    IReadOnlyList<string> ServiceItems,
    string? ServiceNotes);

public sealed record ElderProfileUpdateRequest(
    int? Age,
    string? Gender,
    string CareLevel,
    string RoomNumber,
    string? IdentityCard,
    string? BirthDate,
    string? ElderPhone,
    string FamilyContactName,
    string FamilyContactPhone,
    int? AdlScore,
    string? CognitiveLevel,
    IReadOnlyList<string> MedicalAlerts,
    string? EntrustmentType,
    string? EntrustmentOrganization,
    decimal? MonthlySubsidy,
    IReadOnlyList<string>? ServiceItems,
    string? ServiceNotes);

public sealed record ElderListItemResponse(
    string ElderId,
    string TenantId,
    string ElderName,
    int Age,
    string Gender,
    string CareLevel,
    string RoomNumber,
    string AdmissionStatus,
    string FamilyContactName);

public sealed record ElderListResponse(
    IReadOnlyList<ElderListItemResponse> Items,
    int Total,
    int Page,
    int PageSize);