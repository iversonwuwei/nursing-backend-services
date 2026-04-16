namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record AdminAdmissionOnboardRequest(
    string AdmissionReference,
    string ElderName,
    int Age,
    string Gender,
    string CareLevel,
    string RoomNumber,
    string FamilyContactName,
    string FamilyContactPhone,
    IReadOnlyList<string> MedicalAlerts,
    string BloodPressure,
    int HeartRate,
    decimal Temperature,
    decimal BloodSugar,
    int Oxygen,
    string AlertSummary,
    string? EntrustmentType = null,
    string? EntrustmentOrganization = null,
    decimal? MonthlySubsidy = null,
    IReadOnlyList<string>? ServiceItems = null,
    string? ServiceNotes = null);

public sealed record AdminAdmissionOnboardResponse(
    AdmissionRecordResponse Admission,
    HealthArchiveSummaryResponse HealthArchive,
    CarePlanResponse CarePlan,
    IReadOnlyList<NotificationMessageResponse> Notifications,
    TenantDescriptorResponse Tenant,
    IdentityContextResponse Operator,
    string CorrelationId);

public sealed record FamilyTodaySummaryResponse(
    ElderProfileSummaryResponse Elder,
    HealthArchiveSummaryResponse Health,
    IReadOnlyList<CareTaskResponse> TodayTasks,
    IReadOnlyList<NotificationMessageResponse> Notifications,
    string Narrative);

public sealed record NaniTaskBoardResponse(
    string ElderId,
    string ElderName,
    string CareLevel,
    IReadOnlyList<CareTaskResponse> Tasks,
    IReadOnlyList<NotificationMessageResponse> Notifications);