namespace NursingBackend.BuildingBlocks.Entities;

public sealed class AiAuditLogEntity
{
	public required string AuditId { get; init; }
	public required string TenantId { get; init; }
	public required string UserId { get; init; }
	public required string Capability { get; init; }
	public required string Provider { get; init; }
	public required string Model { get; init; }
	public required string Endpoint { get; init; }
	public required string InputHash { get; init; }
	public required int InputSizeBytes { get; init; }
	public required int OutputSizeBytes { get; init; }
	public required bool Cached { get; init; }
	public required int LatencyMs { get; init; }
	public required bool Success { get; init; }
	public string? ErrorMessage { get; init; }
	public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class AiRuleEntity
{
	public required string RuleId { get; init; }
	public required string TenantId { get; init; }
	public required string RuleCode { get; init; }
	public required string RuleName { get; set; }
	public required string Description { get; set; }
	public required string Capability { get; init; }
	public required bool IsEnabled { get; set; }
	public required int Priority { get; set; }
	public required DateTimeOffset CreatedAtUtc { get; init; }
	public required DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class AiConversationMessageEntity
{
	public required string MessageId { get; init; }
	public required string TenantId { get; init; }
	public required string ConversationId { get; init; }
	public required string UserId { get; init; }
	public required string Role { get; init; } // user | assistant
	public required string Content { get; init; }
	public required DateTimeOffset CreatedAtUtc { get; init; }
}
