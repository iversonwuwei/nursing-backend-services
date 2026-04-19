namespace NursingBackend.BuildingBlocks.Entities;

public sealed class HealthArchiveEntity
{
    public required string ElderId { get; init; }
    public required string TenantId { get; init; }
    public required string ElderName { get; set; }
    public required string BloodPressure { get; set; }
    public required int HeartRate { get; set; }
    public required decimal Temperature { get; set; }
    public required decimal BloodSugar { get; set; }
    public required int Oxygen { get; set; }
    public required string RiskSummary { get; set; }
    public required DateTimeOffset UpdatedAtUtc { get; set; }
}