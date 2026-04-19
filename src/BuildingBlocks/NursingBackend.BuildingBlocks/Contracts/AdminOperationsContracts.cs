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

public sealed record AdminActivityRecordResponse(
	string ActivityId,
	string TenantId,
	string Name,
	string Category,
	string Date,
	string Time,
	int Duration,
	int Participants,
	int Capacity,
	string Location,
	string Status,
	string Teacher,
	string Description,
	string LifecycleStatus,
	string CreatedAt,
	string? PublishedAt,
	string? PublishNote);

public sealed record AdminActivityListResponse(
	IReadOnlyList<AdminActivityRecordResponse> Items,
	int Total,
	int Page,
	int PageSize);

public sealed record AdminActivityCreateRequest(
	string Name,
	string Category,
	string Date,
	string Time,
	int Duration,
	int Capacity,
	string Location,
	string Teacher,
	string Description);

public sealed record AdminActivityActionRequest(
	string Action,
	string? Note);

public sealed record AdminIncidentRecordResponse(
	string IncidentId,
	string TenantId,
	string Title,
	string Level,
	string? ElderName,
	string Room,
	string Reporter,
	string ReporterRole,
	string Time,
	string Status,
	string Description,
	IReadOnlyList<string> Handling,
	string? NextStep,
	IReadOnlyList<string> Attachments,
	string CreatedAt,
	string? AssignedAt,
	string? ClosedAt,
	string? StatusNote);

public sealed record AdminIncidentListResponse(
	IReadOnlyList<AdminIncidentRecordResponse> Items,
	int Total,
	int Page,
	int PageSize);

public sealed record AdminIncidentCreateRequest(
	string Title,
	string Level,
	string? ElderName,
	string Room,
	string Reporter,
	string ReporterRole,
	string Time,
	string Description,
	IReadOnlyList<string> Attachments,
	string? NextStep);

public sealed record AdminIncidentActionRequest(
	string Action,
	string? Note);

public sealed record AdminEquipmentMetricSnapshotResponse(
	int Hr,
	string Bp,
	double Temp,
	int Spo2);

public sealed record AdminEquipmentHistoryPointResponse(
	string Time,
	int Hr,
	int Spo2,
	string Note);

public sealed record AdminEquipmentRecordResponse(
	string EquipmentId,
	string TenantId,
	string Name,
	string Category,
	string Model,
	string SerialNumber,
	string Location,
	string Status,
	string PurchaseDate,
	string MaintenanceDate,
	int MaintenanceCycle,
	string? OrganizationId,
	string? Remarks,
	string Room,
	string Type,
	int Signal,
	int Battery,
	int Uptime,
	AdminEquipmentMetricSnapshotResponse Metrics,
	IReadOnlyList<AdminEquipmentHistoryPointResponse> History,
	string LifecycleStatus,
	string CreatedAt,
	string? ActivatedAt,
	string? AcceptanceNote);

public sealed record AdminEquipmentListResponse(
	IReadOnlyList<AdminEquipmentRecordResponse> Items,
	int Total,
	int Page,
	int PageSize);

public sealed record AdminEquipmentCreateRequest(
	string Name,
	string Category,
	string Model,
	string SerialNumber,
	string Location,
	string PurchaseDate,
	int MaintenanceCycle,
	string? OrganizationId,
	string? Remarks);

public sealed record AdminEquipmentActivateRequest(
	string? AcceptanceNote);

public sealed record AdminSupplyHistoryRecordResponse(
	string Date,
	int In,
	int Out,
	int Balance);

public sealed record AdminSupplyRecordResponse(
	string SupplyId,
	string TenantId,
	string Name,
	string Category,
	string Unit,
	int Stock,
	int MinStock,
	string Price,
	string Supplier,
	string Contact,
	string LastPurchase,
	string Status,
	string LifecycleStatus,
	IReadOnlyList<AdminSupplyHistoryRecordResponse> History,
	string CreatedAt,
	string? ActivatedAt,
	string? IntakeNote,
	int? LastIntakeQuantity);

public sealed record AdminSupplyListResponse(
	IReadOnlyList<AdminSupplyRecordResponse> Items,
	int Total,
	int Page,
	int PageSize);

public sealed record AdminSupplyIntakeRequest(
	string? ExistingId,
	string? Name,
	string? Category,
	string? Unit,
	int Quantity,
	int? MinStock,
	string? Price,
	string? Supplier,
	string? Contact);

public sealed record AdminSupplyActivateRequest(
	string? IntakeNote);

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
