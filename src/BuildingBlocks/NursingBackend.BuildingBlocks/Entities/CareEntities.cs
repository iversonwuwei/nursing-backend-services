namespace NursingBackend.BuildingBlocks.Entities;

public sealed class CarePlanEntity
{
    public required string CarePlanId { get; init; }
    public required string TenantId { get; init; }
    public required string ElderId { get; init; }
    public required string ElderName { get; init; }
    public required string PlanLevel { get; init; }
    public required string Status { get; set; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
}

public sealed class CareTaskEntity
{
    public required string TaskId { get; init; }
    public required string TenantId { get; init; }
    public required string ElderId { get; init; }
    public required string Title { get; init; }
    public required string AssigneeRole { get; init; }
    public required string DueAtLabel { get; init; }
    public required string Status { get; set; }
}

public sealed class ServicePackageEntity
{
    public required string PackageId { get; init; }
    public required string TenantId { get; init; }
    public required string Name { get; set; }
    public required string CareLevel { get; set; }
    public required string TargetGroup { get; set; }
    public required string MonthlyPrice { get; set; }
    public required string SettlementCycle { get; set; }
    public required string ServiceScopeJson { get; set; }
    public required string AddOnsJson { get; set; }
    public required int BoundElders { get; set; }
    public required string Status { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public string? PricingNote { get; set; }
}

public sealed class ServicePlanEntity
{
    public required string PlanId { get; init; }
    public required string TenantId { get; init; }
    public required string PackageId { get; set; }
    public required string PackageName { get; set; }
    public required string ElderlyName { get; set; }
    public required string Room { get; set; }
    public required string CareLevel { get; set; }
    public required string Focus { get; set; }
    public required string ShiftSummary { get; set; }
    public required string OwnerRole { get; set; }
    public required string OwnerName { get; set; }
    public required string RiskTagsJson { get; set; }
    public required string Source { get; set; }
    public required string Status { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public string? ReviewNote { get; set; }
}

public sealed class ServicePlanTaskExecutionEntity
{
    public required string TaskExecutionId { get; init; }
    public required string TenantId { get; init; }
    public required string PlanId { get; init; }
    public required string Status { get; set; }
    public string? HandledBy { get; set; }
    public DateTimeOffset? HandledAtUtc { get; set; }
    public string? ActionNote { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ServicePlanAssignmentEntity
{
    public required string AssignmentId { get; init; }
    public required string TenantId { get; init; }
    public required string PlanId { get; init; }
    public required string ElderlyName { get; set; }
    public required string PackageName { get; set; }
    public required string Room { get; set; }
    public required string StaffName { get; set; }
    public required string StaffRole { get; set; }
    public required string EmploymentSource { get; set; }
    public string? PartnerAgencyName { get; set; }
    public required string DayLabel { get; set; }
    public required string Shift { get; set; }
    public required string Status { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class CareWorkflowAuditEntity
{
    public required string AuditId { get; init; }
    public required string TenantId { get; init; }
    public required string AggregateType { get; init; }
    public required string AggregateId { get; init; }
    public required string ActionType { get; init; }
    public required string OperatorUserId { get; init; }
    public required string OperatorUserName { get; init; }
    public required string CorrelationId { get; init; }
    public string? DetailJson { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}