namespace NursingBackend.BuildingBlocks.Entities;

public sealed class StaffMemberEntity
{
	public string StaffId { get; set; } = default!;
	public string TenantId { get; set; } = default!;
	public string Name { get; set; } = default!;
	public string Role { get; set; } = default!;
	public string Department { get; set; } = default!;
	public string? OrganizationId { get; set; }
	public string? OrganizationName { get; set; }
	public string EmploymentSource { get; set; } = default!;
	public string? PartnerAgencyId { get; set; }
	public string? PartnerAgencyName { get; set; }
	public string? PartnerAffiliationRole { get; set; }
	public string Phone { get; set; } = default!;
	public string Status { get; set; } = default!;
	public string Gender { get; set; } = default!;
	public string Email { get; set; } = default!;
	public int Age { get; set; }
	public int Performance { get; set; }
	public int Attendance { get; set; }
	public int Satisfaction { get; set; }
	public string HireDate { get; set; } = default!;
	public string ScheduleJson { get; set; } = "[]";
	public string CertificatesJson { get; set; } = "[]";
	public string Bonus { get; set; } = default!;
	public string LifecycleStatus { get; set; } = default!;
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ActivatedAtUtc { get; set; }
	public string? OnboardingNote { get; set; }
}