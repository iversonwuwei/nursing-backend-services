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

public sealed record CreateServicePackageRequest(
    string Name,
    string CareLevel,
    string TargetGroup,
    string MonthlyPrice,
    string SettlementCycle,
    IReadOnlyList<string> ServiceScope,
    IReadOnlyList<string> AddOns);

public sealed record ServicePackageActionResponse(
    string PackageId,
    string Status,
    string Message);

public sealed record ServicePackageResponse(
    string Id,
    string Name,
    string CareLevel,
    string TargetGroup,
    string MonthlyPrice,
    string SettlementCycle,
    IReadOnlyList<string> ServiceScope,
    IReadOnlyList<string> AddOns,
    int BoundElders,
    string Status,
    string CreatedAt,
    string? PublishedAt,
    string? PricingNote);

public sealed record CreateServicePlanRequest(
    string PackageId,
    string ElderlyName,
    string Room,
    string Focus,
    string Shift,
    string OwnerRole,
    string OwnerName,
    IReadOnlyList<string> RiskTags,
    string Source);

public sealed record ServicePlanActionResponse(
    string PlanId,
    string Status,
    string Message);

public sealed record ServicePlanResponse(
    string Id,
    string ElderlyName,
    string Room,
    string PackageId,
    string PackageName,
    string CareLevel,
    string Focus,
    string Shift,
    string OwnerRole,
    string OwnerName,
    IReadOnlyList<string> RiskTags,
    string Source,
    string Status,
    string CreatedAt,
    string? ReviewNote);

public sealed record ServicePlanTaskActionRequest(
    string? HandledBy,
    string? ActionNote);

public sealed record SaveServicePlanTaskNoteRequest(
    string Status,
    string ActionNote,
    string? HandledBy,
    string? HandledAt,
    string? HandledAtIso);

public sealed record ServicePlanTaskResponse(
    string Id,
    string PlanId,
    string ElderlyName,
    string Room,
    string Title,
    string Owner,
    string OwnerName,
    string OwnerRole,
    string Reminder,
    string ScheduledTime,
    string Shift,
    string CareLevel,
    string Priority,
    string Status,
    string SourceId,
    string SourceStatus,
    string OriginStatusLabel,
    string OriginLabel,
    string PackageName,
    string? HandledBy,
    string? HandledAt,
    string? HandledAtIso,
    string? ActionNote);

public sealed record CareScheduleAssignmentResponse(
    string AssignmentId,
    string PlanId,
    string Shift,
    string ElderlyName,
    string PackageName,
    string Room,
    string Status);

public sealed record CareScheduleCellResponse(
    string DayLabel,
    IReadOnlyList<CareScheduleAssignmentResponse> Assignments);

public sealed record CareScheduleStaffRowResponse(
    string StaffId,
    string StaffName,
    string StaffRole,
    string EmploymentSource,
    string? PartnerAgencyName,
    int AssignedPlans,
    int ExceptionPlans,
    int PendingReviewPlans,
    IReadOnlyList<CareScheduleCellResponse> Cells);

public sealed record CareScheduleDaySummaryResponse(
    string DayLabel,
    IReadOnlyList<CareScheduleShiftSummaryResponse> Shifts);

public sealed record CareScheduleShiftSummaryResponse(
    string Shift,
    int Count);

public sealed record CareScheduleAttentionPlanResponse(
    string Id,
    string ElderlyName,
    string PackageName,
    string OwnerRole,
    string OwnerName,
    string Shift,
    string Status);

public sealed record CareScheduleBoardResponse(
    string WeekLabel,
    int ActivePlans,
    int PendingReviewPlans,
    int UnassignedPlans,
    int ThirdPartyAssignedPlans,
    int PublishedAssignments,
    IReadOnlyList<CareScheduleShiftSummaryResponse> ShiftDemand,
    IReadOnlyList<CareScheduleStaffRowResponse> StaffRows,
    IReadOnlyList<CareScheduleDaySummaryResponse> DaySummaries,
    IReadOnlyList<CareScheduleAttentionPlanResponse> AttentionPlans);

public sealed record CareWorkflowObservabilityResponse(
    int PendingReviewPlans,
    int UnassignedPlans,
    int ArchivedPlans,
    int CompletedTasks,
    int AuditRecords,
    long TaskCompletionTotal,
    long PlanArchiveTotal,
    long UnassignedBacklogGauge);

public sealed record CareWorkflowAuditResponse(
    string AuditId,
    string AggregateType,
    string AggregateId,
    string ActionType,
    string OperatorUserName,
    string CorrelationId,
    string? DetailJson,
    string CreatedAt);

public sealed record NursingWorkflowBoardResponse(
    IReadOnlyList<ServicePackageResponse> Packages,
    IReadOnlyList<ServicePlanResponse> Plans,
    IReadOnlyList<ServicePlanTaskResponse> Tasks,
    CareScheduleBoardResponse Schedule,
    CareWorkflowObservabilityResponse Observability);