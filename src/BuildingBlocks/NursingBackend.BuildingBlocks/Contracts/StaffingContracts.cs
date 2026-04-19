namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record AdminStaffScheduleItemResponse(
	string Day,
	string Shift);

public sealed record AdminStaffRecordResponse(
	string StaffId,
	string TenantId,
	string Name,
	string Role,
	string Department,
	string? OrganizationId,
	string? OrganizationName,
	string EmploymentSource,
	string? PartnerAgencyId,
	string? PartnerAgencyName,
	string? PartnerAffiliationRole,
	string Phone,
	string Status,
	string Gender,
	string Email,
	int Age,
	int Performance,
	int Attendance,
	int Satisfaction,
	string HireDate,
	IReadOnlyList<AdminStaffScheduleItemResponse> Schedule,
	IReadOnlyList<string> Certificates,
	string Bonus,
	string LifecycleStatus,
	string CreatedAt,
	string? ActivatedAt,
	string? OnboardingNote);

public sealed record AdminStaffListResponse(
	IReadOnlyList<AdminStaffRecordResponse> Items,
	int Total,
	int Page,
	int PageSize);

public sealed record AdminStaffCreateRequest(
	string Name,
	string Role,
	string Department,
	string? OrganizationId,
	string? OrganizationName,
	string EmploymentSource,
	string? PartnerAgencyId,
	string? PartnerAgencyName,
	string? PartnerAffiliationRole,
	string Phone,
	string Gender,
	string Email,
	int Age,
	string HireDate);

public sealed record AdminStaffActivateRequest(
	string? OnboardingNote);