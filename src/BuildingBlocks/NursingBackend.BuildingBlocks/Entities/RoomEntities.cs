namespace NursingBackend.BuildingBlocks.Entities;

public sealed class RoomEntity
{
	public string RoomId { get; set; } = default!;
	public string TenantId { get; set; } = default!;
	public string Name { get; set; } = default!;
	public int Floor { get; set; }
	public string FloorName { get; set; } = default!;
	public string Type { get; set; } = default!;
	public int Capacity { get; set; }
	public string Status { get; set; } = default!;
	public string? OrganizationId { get; set; }
	public string OrganizationName { get; set; } = default!;
	public string FacilitiesJson { get; set; } = "[]";
	public string CleanStatus { get; set; } = default!;
	public string LastClean { get; set; } = default!;
	public string NextClean { get; set; } = default!;
	public string LifecycleStatus { get; set; } = default!;
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ActivatedAtUtc { get; set; }
	public string? ActivationNote { get; set; }
}