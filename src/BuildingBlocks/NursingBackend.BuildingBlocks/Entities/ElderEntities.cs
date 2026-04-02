namespace NursingBackend.BuildingBlocks.Entities;

public sealed class AdmissionRecordEntity
{
    public required string AdmissionId { get; init; }
    public required string TenantId { get; init; }
    public required string ElderId { get; init; }
    public required string AdmissionReference { get; init; }
    public required string Status { get; set; }
    public required string CareLevel { get; init; }
    public required string RoomNumber { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class ElderProfileEntity
{
    public required string ElderId { get; init; }
    public required string TenantId { get; init; }
    public required string ElderName { get; init; }
    public required int Age { get; init; }
    public required string Gender { get; init; }
    public required string CareLevel { get; set; }
    public required string RoomNumber { get; set; }
    public required string FamilyContactName { get; init; }
    public required string FamilyContactPhone { get; init; }
    public required List<string> MedicalAlerts { get; init; }
    public required string AdmissionStatus { get; set; }
}