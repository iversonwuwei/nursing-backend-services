namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record NotificationDispatchRequest(
    string Audience,
    string AudienceKey,
    string Category,
    string Title,
    string Body,
    string SourceService,
    string SourceEntityId,
    string CorrelationId);

public sealed record NotificationMessageResponse(
    string NotificationId,
    string TenantId,
    string Audience,
    string AudienceKey,
    string Category,
    string Title,
    string Body,
    string SourceService,
    string SourceEntityId,
    DateTimeOffset CreatedAtUtc,
    string Status);

public sealed record NotificationDeliveryResultRequest(
    string Status,
    string Channel,
    string? FailureCode,
    string? FailureReason);

public sealed record NotificationProviderCallbackRequest(
    string Provider,
    string Channel,
    string Status,
    string? NotificationId,
    string? CorrelationId,
    string? SourceService,
    string? SourceEntityId,
    string? ProviderMessageId,
    string? FailureCode,
    string? FailureReason,
    DateTimeOffset? OccurredAtUtc);

public sealed record NotificationDeliveryAttemptResponse(
    string DeliveryAttemptId,
    string NotificationId,
    string Channel,
    string Status,
    string? FailureCode,
    string? FailureReason,
    string CompensationStatus,
    string? CompensationReferenceId,
    DateTimeOffset AttemptedAtUtc);

public sealed record NotificationObservabilityResponse(
    int Queued,
    int Delivered,
    int Failed,
    int CompensationRequested,
    int CompensationFailed,
    DateTimeOffset GeneratedAtUtc);