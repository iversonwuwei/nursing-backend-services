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