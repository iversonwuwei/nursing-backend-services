namespace NursingBackend.BuildingBlocks.Entities;

public sealed class OrganizationEntity
{
	public string OrganizationId { get; set; } = default!;
	public string TenantId { get; set; } = default!;
	public string Name { get; set; } = default!;
	public string Address { get; set; } = default!;
	public string Phone { get; set; } = default!;
	public string Status { get; set; } = default!;
	public string EstablishedDate { get; set; } = default!;
	public string Manager { get; set; } = default!;
	public string ManagerPhone { get; set; } = default!;
	public string Description { get; set; } = default!;
	public string LifecycleStatus { get; set; } = default!;
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ActivatedAtUtc { get; set; }
	public string? ActivationNote { get; set; }
}