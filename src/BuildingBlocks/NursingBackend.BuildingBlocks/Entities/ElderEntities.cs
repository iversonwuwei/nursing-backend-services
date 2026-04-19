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
    public string AssessmentStatus { get; set; } = string.Empty;
    public string RequestedCareLevel { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string EmergencyContact { get; set; } = string.Empty;
    public string ChronicConditions { get; set; } = string.Empty;
    public string MedicationSummary { get; set; } = string.Empty;
    public string AllergySummary { get; set; } = string.Empty;
    public int AdlScore { get; set; }
    public string CognitiveLevel { get; set; } = string.Empty;
    public string RiskNotes { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceLabel { get; set; }
    public List<string> SourceDocumentNames { get; set; } = [];
    public string? SourceSummary { get; set; }
    public string AiRecommendedCareLevel { get; set; } = string.Empty;
    public int AiConfidence { get; set; }
    public int AiAssessmentScore { get; set; }
    public string AiReasonSummary { get; set; } = string.Empty;
    public List<string> AiReasons { get; set; } = [];
    public List<string> AiFocusTags { get; set; } = [];
    public string AiPlanTemplateCode { get; set; } = string.Empty;
    public string? ConfirmedCareLevel { get; set; }
    public string? ReviewNote { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public string? ConfirmedBy { get; set; }
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