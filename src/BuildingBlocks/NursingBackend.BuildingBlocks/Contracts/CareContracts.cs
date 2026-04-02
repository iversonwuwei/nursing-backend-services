namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record CarePlanCreateFromAdmissionRequest(
    string AdmissionId,
    string ElderId,
    string ElderName,
    string CareLevel,
    string RoomNumber);

public sealed record CareTaskResponse(
    string TaskId,
    string ElderId,
    string Title,
    string AssigneeRole,
    string DueAtLabel,
    string Status);

public sealed record CarePlanResponse(
    string CarePlanId,
    string ElderId,
    string TenantId,
    string PlanLevel,
    string Status,
    IReadOnlyList<CareTaskResponse> Tasks,
    DateTimeOffset GeneratedAtUtc);

public sealed record NaniTaskFeedResponse(
    string ElderId,
    string ElderName,
    string CareLevel,
    IReadOnlyList<CareTaskResponse> Tasks);