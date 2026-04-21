namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record AdmissionCreateRequest(
    string AdmissionReference,
    string ElderName,
    int Age,
    string Gender,
    string CareLevel,
    string RoomNumber,
    string FamilyContactName,
    string FamilyContactPhone,
    IReadOnlyList<string> MedicalAlerts,
    string? IdentityCard = null,
    string? BirthDate = null,
    string? ElderPhone = null,
    int? AdlScore = null,
    string? CognitiveLevel = null,
    string? EntrustmentType = null,
    string? EntrustmentOrganization = null,
    decimal? MonthlySubsidy = null,
    IReadOnlyList<string>? ServiceItems = null,
    string? ServiceNotes = null);

public sealed record AdmissionRecordResponse(
    string AdmissionId,
    string ElderId,
    string TenantId,
    string ElderName,
    string CareLevel,
    string RoomNumber,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record AssessmentAiRecommendationResponse(
    string RecommendedLevel,
    int Confidence,
    int AssessmentScore,
    string ReasonSummary,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> FocusTags,
    string PlanTemplateCode);

public sealed record AdminAssessmentCaseCreateRequest(
    string ElderName,
    int Age,
    string Gender,
    string Phone,
    string EmergencyContact,
    string RoomNumber,
    string RequestedCareLevel,
    string ChronicConditions,
    string MedicationSummary,
    string AllergySummary,
    int AdlScore,
    string CognitiveLevel,
    string RiskNotes,
    string? EntrustmentType,
    string? EntrustmentOrganization,
    decimal? MonthlySubsidy,
    IReadOnlyList<string>? ServiceItems,
    string? ServiceNotes,
    string SourceType,
    string? SourceLabel,
    IReadOnlyList<string>? SourceDocumentNames,
    string? SourceSummary);

public sealed record AssessmentCaseCreateRequest(
    string ElderName,
    int Age,
    string Gender,
    string Phone,
    string EmergencyContact,
    string RoomNumber,
    string RequestedCareLevel,
    string ChronicConditions,
    string MedicationSummary,
    string AllergySummary,
    int AdlScore,
    string CognitiveLevel,
    string RiskNotes,
    string? EntrustmentType,
    string? EntrustmentOrganization,
    decimal? MonthlySubsidy,
    IReadOnlyList<string>? ServiceItems,
    string? ServiceNotes,
    string SourceType,
    string? SourceLabel,
    IReadOnlyList<string>? SourceDocumentNames,
    string? SourceSummary,
    AssessmentAiRecommendationResponse AiRecommendation);

public sealed record AssessmentDecisionUpdateRequest(
    string ConfirmedCareLevel,
    string? ReviewNote,
    string ConfirmedBy);

public sealed record AssessmentCaseResponse(
    string AssessmentId,
    string ElderId,
    string TenantId,
    string ElderName,
    int Age,
    string Gender,
    string RoomNumber,
    string Phone,
    string EmergencyContact,
    string RequestedCareLevel,
    string Status,
    string ChronicConditions,
    string MedicationSummary,
    string AllergySummary,
    int AdlScore,
    string CognitiveLevel,
    string RiskNotes,
    string? EntrustmentType,
    string? EntrustmentOrganization,
    decimal? MonthlySubsidy,
    IReadOnlyList<string> ServiceItems,
    string? ServiceNotes,
    string SourceType,
    string SourceLabel,
    IReadOnlyList<string> SourceDocumentNames,
    string? SourceSummary,
    AssessmentAiRecommendationResponse AiRecommendation,
    string? ConfirmedCareLevel,
    string? ReviewNote,
    DateTimeOffset? ConfirmedAtUtc,
    string? ConfirmedBy,
    DateTimeOffset CreatedAtUtc);

public sealed record AssessmentCaseListResponse(
    IReadOnlyList<AssessmentCaseResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record ElderProfileSummaryResponse(
    string ElderId,
    string TenantId,
    string ElderName,
    int Age,
    string Gender,
    string CareLevel,
    string RoomNumber,
    string AdmissionStatus,
    string? IdentityCard,
    string? BirthDate,
    string? ElderPhone,
    string FamilyContactName,
    string FamilyContactPhone,
    int? AdlScore,
    string? CognitiveLevel,
    IReadOnlyList<string> MedicalAlerts,
    string? EntrustmentType,
    string? EntrustmentOrganization,
    decimal? MonthlySubsidy,
    IReadOnlyList<string> ServiceItems,
    string? ServiceNotes);

public sealed record ElderProfileUpdateRequest(
    int? Age,
    string? Gender,
    string CareLevel,
    string RoomNumber,
    string? IdentityCard,
    string? BirthDate,
    string? ElderPhone,
    string FamilyContactName,
    string FamilyContactPhone,
    int? AdlScore,
    string? CognitiveLevel,
    IReadOnlyList<string> MedicalAlerts,
    string? EntrustmentType,
    string? EntrustmentOrganization,
    decimal? MonthlySubsidy,
    IReadOnlyList<string>? ServiceItems,
    string? ServiceNotes);

public sealed record ElderListItemResponse(
    string ElderId,
    string TenantId,
    string ElderName,
    int Age,
    string Gender,
    string CareLevel,
    string RoomNumber,
    string AdmissionStatus,
    string FamilyContactName,
    DateTimeOffset? AdmissionCreatedAtUtc);

public sealed record ElderListResponse(
    IReadOnlyList<ElderListItemResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record ElderFaceEnrollmentListItemResponse(
    string ElderId,
    string TenantId,
    string ElderName,
    string RoomNumber,
    string CareLevel,
    string FaceEnrollmentStatus,
    IReadOnlyList<string> FaceCapturedSteps,
    int FaceQualityScore,
    string FaceQualitySummary,
    string? FaceOperator,
    string? FaceDeviceLabel,
    string? FaceEntrySource,
    DateTimeOffset? FaceLastUpdatedUtc,
    DateTimeOffset? FaceActivatedAtUtc,
    string? FaceActivationNote,
    string? FaceRetakeReason);

public sealed record ElderFaceEnrollmentListResponse(
    IReadOnlyList<ElderFaceEnrollmentListItemResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record ElderFaceEnrollmentUpdateRequest(
    string Operator,
    string DeviceLabel,
    string EntrySource);

public sealed record ElderFaceCaptureRequest(
    string Step,
    string Operator,
    string DeviceLabel);

public sealed record ElderFaceActivationRequest(
    string ActivationNote);

public sealed record ElderFaceRetakeRequest(
    string Reason);