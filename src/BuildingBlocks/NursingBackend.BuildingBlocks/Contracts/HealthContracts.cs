namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record HealthArchiveCreateFromAdmissionRequest(
    string AdmissionId,
    string ElderId,
    string ElderName,
    string CareLevel,
    string BloodPressure,
    int HeartRate,
    decimal Temperature,
    decimal BloodSugar,
    int Oxygen,
    string AlertSummary);

public sealed record HealthArchiveSummaryResponse(
    string ElderId,
    string TenantId,
    string ElderName,
    string BloodPressure,
    int HeartRate,
    decimal Temperature,
    decimal BloodSugar,
    int Oxygen,
    string RiskSummary,
    DateTimeOffset UpdatedAtUtc);

public sealed record HealthArchiveCreateRequest(
    string ElderId,
    string ElderName,
    string BloodPressure,
    int HeartRate,
    decimal Temperature,
    decimal BloodSugar,
    int Oxygen,
    string RiskSummary);

public sealed record HealthArchiveListItemResponse(
    string ElderId,
    string TenantId,
    string ElderName,
    string BloodPressure,
    int HeartRate,
    decimal Temperature,
    decimal BloodSugar,
    int Oxygen,
    string RiskSummary,
    DateTimeOffset UpdatedAtUtc);

public sealed record HealthArchiveListResponse(
    IReadOnlyList<HealthArchiveListItemResponse> Items,
    DateTimeOffset GeneratedAtUtc);

public sealed record AdminHealthArchiveCreateRequest(
    string ElderId,
    string BloodPressure,
    int HeartRate,
    decimal Temperature,
    decimal BloodSugar,
    int Oxygen,
    string? RiskSummary);

public sealed record AdminHealthArchiveListItemResponse(
    string ElderId,
    string TenantId,
    string ElderName,
    string RoomNumber,
    int Age,
    string CareLevel,
    string AdmissionStatus,
    string BloodPressure,
    int HeartRate,
    decimal Temperature,
    decimal BloodSugar,
    int Oxygen,
    string RiskSummary,
    DateTimeOffset UpdatedAtUtc);

public sealed record AdminHealthArchiveListResponse(
    IReadOnlyList<AdminHealthArchiveListItemResponse> Items,
    DateTimeOffset GeneratedAtUtc);