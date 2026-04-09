namespace NursingBackend.BuildingBlocks.Contracts;

// ── AI Request / Response Envelope ──────────────────────────────────────────

public sealed record AiCompletionRequest(
	string Capability,
	string Prompt,
	Dictionary<string, object>? Context = null,
	string? ConversationId = null);

public sealed record AiResult<T>(
	bool Available,
	string Capability,
	string Provider,
	string Model,
	T? Result,
	bool Cached,
	int LatencyMs,
	string TraceId,
	string AuditId);

public sealed record AiTextResult(string Text);

// ── Dashboard Insights ──────────────────────────────────────────────────────

public sealed record AiDashboardInsightsRequest(
	int TotalElders,
	int ActiveCarePlans,
	int OpenAlerts,
	int PendingTasks,
	int OccupancyPercent,
	string? AdditionalContext);

public sealed record AiDashboardInsightsResponse(
	string Summary,
	IReadOnlyList<string> KeyInsights,
	IReadOnlyList<string> ActionItems);

// ── Health Risk ─────────────────────────────────────────────────────────────

public sealed record AiHealthRiskRequest(
	string ElderId,
	string ElderName,
	string BloodPressure,
	int HeartRate,
	decimal Temperature,
	decimal BloodSugar,
	int Oxygen,
	string? CurrentMedications,
	string? MedicalHistory);

public sealed record AiHealthRiskResponse(
	string RiskLevel,
	string Explanation,
	IReadOnlyList<string> Recommendations,
	IReadOnlyList<string> MonitoringPoints);

// ── Alert Suggestion ────────────────────────────────────────────────────────

public sealed record AiAlertSuggestionRequest(
	string AlertType,
	string AlertDescription,
	string Severity,
	string? ElderContext,
	string? RecentHistory);

public sealed record AiAlertSuggestionResponse(
	string SuggestedAction,
	string Rationale,
	string Priority,
	IReadOnlyList<string> Steps);

// ── Task Priority ───────────────────────────────────────────────────────────

public sealed record AiTaskPriorityRequest(
	IReadOnlyList<AiTaskPriorityItem> Tasks);

public sealed record AiTaskPriorityItem(
	string TaskId,
	string Title,
	string ElderName,
	string CareLevel,
	string DueAt,
	string Status);

public sealed record AiTaskPriorityResponse(
	IReadOnlyList<AiTaskPriorityRankedItem> RankedTasks,
	string Rationale);

public sealed record AiTaskPriorityRankedItem(
	string TaskId,
	int Rank,
	string Priority,
	string Reason);

// ── Admission Assessment ────────────────────────────────────────────────────

public sealed record AiAdmissionAssessmentRequest(
	string ElderName,
	int Age,
	string Gender,
	string RequestedCareLevel,
	IReadOnlyList<string> MedicalAlerts,
	string? FamilyNotes);

public sealed record AiAdmissionAssessmentResponse(
	string RecommendedCareLevel,
	string AssessmentSummary,
	IReadOnlyList<string> RiskFactors,
	IReadOnlyList<string> Recommendations);

// ── Ops Report ──────────────────────────────────────────────────────────────

public sealed record AiOpsReportRequest(
	string ReportType,
	string Period,
	string? MetricsJson);

public sealed record AiOpsReportResponse(
	string ReportTitle,
	string Summary,
	IReadOnlyList<string> Highlights,
	IReadOnlyList<string> Concerns,
	IReadOnlyList<string> Recommendations);

// ── Resource Insights ───────────────────────────────────────────────────────

public sealed record AiResourceInsightsRequest(
	string ResourceType,
	string? DataSummary);

public sealed record AiResourceInsightsResponse(
	string Summary,
	IReadOnlyList<string> Insights,
	IReadOnlyList<string> Suggestions);

// ── Chat ────────────────────────────────────────────────────────────────────

public sealed record AiChatRequest(
	string Message,
	string? ConversationId,
	string? UserRole);

public sealed record AiChatResponse(
	string Reply,
	string ConversationId);

// ── Shift Summary (Nani) ────────────────────────────────────────────────────

public sealed record AiShiftSummaryRequest(
	string Shift,
	int CompletedTasks,
	int PendingTasks,
	int Alerts,
	string? Notes);

public sealed record AiShiftSummaryResponse(
	string Summary,
	IReadOnlyList<string> KeyPoints,
	IReadOnlyList<string> HandoverItems);

// ── Handover Draft (Nani) ───────────────────────────────────────────────────

public sealed record AiHandoverDraftRequest(
	string FromShift,
	string ToShift,
	IReadOnlyList<string> CompletedItems,
	IReadOnlyList<string> PendingItems,
	IReadOnlyList<string> Alerts);

public sealed record AiHandoverDraftResponse(
	string Draft,
	IReadOnlyList<string> CriticalItems);

// ── Escalation Draft (Nani) ─────────────────────────────────────────────────

public sealed record AiEscalationDraftRequest(
	string AlertType,
	string ElderName,
	string Description,
	string CurrentStatus);

public sealed record AiEscalationDraftResponse(
	string Draft,
	string SuggestedRecipient,
	string Priority);

// ── Today Summary (Family) ──────────────────────────────────────────────────

public sealed record AiFamilyTodaySummaryRequest(
	string ElderName,
	string CareLevel,
	string HealthSummary,
	int CompletedTasks,
	int PendingTasks,
	IReadOnlyList<string> RecentNotes);

public sealed record AiFamilyTodaySummaryResponse(
	string Summary,
	IReadOnlyList<AiFamilyQaItem> FrequentQuestions);

public sealed record AiFamilyQaItem(
	string Question,
	string Answer);

// ── Health Explain (Family) ─────────────────────────────────────────────────

public sealed record AiHealthExplainRequest(
	string ElderName,
	string MetricName,
	string MetricValue,
	string NormalRange,
	string? TrendDescription);

public sealed record AiHealthExplainResponse(
	string Explanation,
	string Recommendation);

// ── Visit Assistant (Family) ────────────────────────────────────────────────

public sealed record AiVisitAssistantRequest(
	string ElderName,
	string CareLevel,
	string? RecentHealthSummary,
	IReadOnlyList<string>? PreferredTimeSlots);

public sealed record AiVisitAssistantResponse(
	IReadOnlyList<string> SuggestedTimeSlots,
	IReadOnlyList<string> VisitTips,
	string Recommendation);

// ── Visit Risk (Family) ────────────────────────────────────────────────────

public sealed record AiVisitRiskRequest(
	string ElderName,
	string TimeSlot,
	string? CurrentHealthStatus);

public sealed record AiVisitRiskResponse(
	string RiskLevel,
	string Analysis,
	IReadOnlyList<string> Precautions);

// ── AI Rules (Governance) ───────────────────────────────────────────────────

public sealed record AiRuleResponse(
	string RuleId,
	string RuleCode,
	string RuleName,
	string Description,
	string Capability,
	bool IsEnabled,
	int Priority,
	DateTimeOffset UpdatedAtUtc);

public sealed record AiRuleToggleRequest(
	bool IsEnabled);

// ── AI Model Status (Governance) ────────────────────────────────────────────

public sealed record AiModelStatusResponse(
	string Provider,
	string Model,
	string Capability,
	bool IsReachable,
	int? LatencyMs,
	DateTimeOffset CheckedAtUtc,
	string? ConfiguredProvider,
	string? ConfiguredModel,
	bool UsesProviderDefaultModel,
	string ConfigurationSource);

// ── AI Audit Log (Governance) ───────────────────────────────────────────────

public sealed record AiAuditLogResponse(
	string AuditId,
	string TenantId,
	string UserId,
	string Capability,
	string Provider,
	string Model,
	string Endpoint,
	bool Cached,
	int LatencyMs,
	bool Success,
	string? ErrorMessage,
	DateTimeOffset CreatedAtUtc);

public sealed record AiAuditLogListResponse(
	IReadOnlyList<AiAuditLogResponse> Items,
	int Total,
	int Page,
	int PageSize);
