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
    IReadOnlyList<string> MedicalAlerts);

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
    string CareLevel,
    string RoomNumber,
    string AdmissionStatus,
    string FamilyContactName,
    IReadOnlyList<string> MedicalAlerts);

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