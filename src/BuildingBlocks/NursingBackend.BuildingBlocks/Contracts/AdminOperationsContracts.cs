namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record AdminAlertModuleSummaryResponse(
	string Module,
	int Pending,
	int Processing,
	int Resolved,
	int Critical);

public sealed record AdminAlertSummaryResponse(
	IReadOnlyList<AdminAlertModuleSummaryResponse> Modules,
	DateTimeOffset GeneratedAtUtc);

public sealed record AdminAlertQueueItemResponse(
	string AlertId,
	string Module,
	string Type,
	string Level,
	string Status,
	string ElderId,
	string ElderlyName,
	string RoomNumber,
	string Description,
	string? DeviceName,
	string OccurredAt,
	string? HandledBy,
	string? HandledAt,
	string? Resolution);

public sealed record AdminAlertActionRequest(
	string Action,
	string? Note);

public sealed record AdminFinanceSummaryResponse(
	int PendingReview,
	int Issued,
	int Overdue,
	int PendingArchive,
	int ActionRequired,
	int FailedNotifications,
	DateTimeOffset GeneratedAtUtc);

public sealed record AdminNotificationSummaryResponse(
	int Queued,
	int Delivered,
	int Failed,
	int Broadcasts,
	int VisitNotices,
	int ScheduledReminders,
	DateTimeOffset GeneratedAtUtc);
