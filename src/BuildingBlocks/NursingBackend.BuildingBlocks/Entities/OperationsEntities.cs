namespace NursingBackend.BuildingBlocks.Entities;

public sealed class AlertCaseEntity
{
	public string AlertId { get; set; } = default!;
	public string TenantId { get; set; } = default!;
	public string Module { get; set; } = default!;
	public string Type { get; set; } = default!;
	public string Level { get; set; } = default!;
	public string Status { get; set; } = default!;
	public string ElderId { get; set; } = default!;
	public string ElderlyName { get; set; } = default!;
	public string RoomNumber { get; set; } = default!;
	public string Description { get; set; } = default!;
	public string? DeviceName { get; set; }
	public DateTimeOffset OccurredAtUtc { get; set; }
	public string? HandledBy { get; set; }
	public DateTimeOffset? HandledAtUtc { get; set; }
	public string? Resolution { get; set; }
}

public sealed class ActivityEntity
{
	public string ActivityId { get; set; } = default!;
	public string TenantId { get; set; } = default!;
	public string Name { get; set; } = default!;
	public string Category { get; set; } = default!;
	public string Date { get; set; } = default!;
	public string Time { get; set; } = default!;
	public int Duration { get; set; }
	public int Participants { get; set; }
	public int Capacity { get; set; }
	public string Location { get; set; } = default!;
	public string Status { get; set; } = default!;
	public string Teacher { get; set; } = default!;
	public string Description { get; set; } = default!;
	public string LifecycleStatus { get; set; } = default!;
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? PublishedAtUtc { get; set; }
	public string? PublishNote { get; set; }
}

public sealed class IncidentEntity
{
	public string IncidentId { get; set; } = default!;
	public string TenantId { get; set; } = default!;
	public string Title { get; set; } = default!;
	public string Level { get; set; } = default!;
	public string? ElderName { get; set; }
	public string Room { get; set; } = default!;
	public string Reporter { get; set; } = default!;
	public string ReporterRole { get; set; } = default!;
	public DateTimeOffset OccurredAtUtc { get; set; }
	public string Status { get; set; } = default!;
	public string Description { get; set; } = default!;
	public string HandlingJson { get; set; } = "[]";
	public string? NextStep { get; set; }
	public string AttachmentsJson { get; set; } = "[]";
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? AssignedAtUtc { get; set; }
	public DateTimeOffset? ClosedAtUtc { get; set; }
	public string? StatusNote { get; set; }
}

public sealed class EquipmentEntity
{
	public string EquipmentId { get; set; } = default!;
	public string TenantId { get; set; } = default!;
	public string Name { get; set; } = default!;
	public string Category { get; set; } = default!;
	public string Model { get; set; } = default!;
	public string SerialNumber { get; set; } = default!;
	public string Location { get; set; } = default!;
	public string Status { get; set; } = default!;
	public string PurchaseDate { get; set; } = default!;
	public string MaintenanceDate { get; set; } = default!;
	public int MaintenanceCycle { get; set; }
	public string? OrganizationId { get; set; }
	public string? Remarks { get; set; }
	public string Room { get; set; } = default!;
	public string Type { get; set; } = default!;
	public int Signal { get; set; }
	public int Battery { get; set; }
	public int Uptime { get; set; }
	public int MetricsHr { get; set; }
	public string MetricsBp { get; set; } = default!;
	public double MetricsTemp { get; set; }
	public int MetricsSpo2 { get; set; }
	public string HistoryJson { get; set; } = "[]";
	public string LifecycleStatus { get; set; } = default!;
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ActivatedAtUtc { get; set; }
	public string? AcceptanceNote { get; set; }
}

public sealed class SupplyEntity
{
	public string SupplyId { get; set; } = default!;
	public string TenantId { get; set; } = default!;
	public string Name { get; set; } = default!;
	public string Category { get; set; } = default!;
	public string Unit { get; set; } = default!;
	public int Stock { get; set; }
	public int MinStock { get; set; }
	public string Price { get; set; } = default!;
	public string Supplier { get; set; } = default!;
	public string Contact { get; set; } = default!;
	public string LastPurchase { get; set; } = default!;
	public string Status { get; set; } = default!;
	public string LifecycleStatus { get; set; } = default!;
	public string HistoryJson { get; set; } = "[]";
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ActivatedAtUtc { get; set; }
	public string? IntakeNote { get; set; }
	public int? LastIntakeQuantity { get; set; }
}