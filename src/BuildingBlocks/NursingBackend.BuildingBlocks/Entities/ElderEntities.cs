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
    public required int Age { get; set; }
    public required string Gender { get; set; }
    public required string CareLevel { get; set; }
    public required string RoomNumber { get; set; }
    public string? IdentityCard { get; set; }
    public string? BirthDate { get; set; }
    public string? ElderPhone { get; set; }
    public required string FamilyContactName { get; set; }
    public required string FamilyContactPhone { get; set; }
    public int? AdlScore { get; set; }
    public string? CognitiveLevel { get; set; }
    public required List<string> MedicalAlerts { get; set; }
    public required string AdmissionStatus { get; set; }
    public string? EntrustmentType { get; set; }
    public string? EntrustmentOrganization { get; set; }
    public decimal? MonthlySubsidy { get; set; }
    public List<string> ServiceItems { get; set; } = [];
    public string? ServiceNotes { get; set; }
}