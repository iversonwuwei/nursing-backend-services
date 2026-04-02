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