namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record BillingInvoiceCreateRequest(
	string ElderId,
	string ElderName,
	string PackageName,
	decimal Amount,
	DateTimeOffset DueAtUtc);

public sealed record BillingInvoiceResponse(
	string InvoiceId,
	string TenantId,
	string ElderId,
	string ElderName,
	string PackageName,
	decimal Amount,
	DateTimeOffset DueAtUtc,
	string Status,
	string NotificationStatus,
	DateTimeOffset CreatedAtUtc,
	DateTimeOffset? UpdatedAtUtc);

public sealed record BillingNotificationCompensationRequest(
	string NotificationId,
	string CorrelationId,
	string FailureCode,
	string FailureReason);

public sealed record BillingCompensationResolveRequest(
	string ResolutionNote,
	string RestoredInvoiceStatus);

public sealed record BillingCompensationResponse(
	string CompensationId,
	string InvoiceId,
	string NotificationId,
	string Status,
	string FailureCode,
	string FailureReason,
	DateTimeOffset CreatedAtUtc,
	DateTimeOffset? ResolvedAtUtc);

public sealed record BillingObservabilityResponse(
	int PendingOutbox,
	int ActionRequiredInvoices,
	int OpenCompensations,
	int OverdueInvoices,
	int FailedNotificationInvoices,
	DateTimeOffset GeneratedAtUtc);