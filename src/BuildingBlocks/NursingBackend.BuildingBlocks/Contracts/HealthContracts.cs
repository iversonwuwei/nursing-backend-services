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