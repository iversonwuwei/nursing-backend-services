namespace NursingBackend.BuildingBlocks.Entities;

public sealed class VisitAppointmentEntity
{
    public required string VisitId { get; init; }
    public required string TenantId { get; init; }
    public required string ElderId { get; init; }
    public required string VisitorName { get; init; }
    public required string Relation { get; init; }
    public required string Phone { get; init; }
    public required DateTimeOffset PlannedAtUtc { get; init; }
    public required string VisitType { get; init; }
    public required string Notes { get; init; }
    public required string Status { get; set; }
}