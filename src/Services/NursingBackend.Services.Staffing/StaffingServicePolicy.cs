using System.Text.Json;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Staffing;

internal static class StaffingServicePolicy
{
	public static string? ValidateCreateRequest(AdminStaffCreateRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Name)
			|| string.IsNullOrWhiteSpace(request.Role)
			|| string.IsNullOrWhiteSpace(request.Department)
			|| string.IsNullOrWhiteSpace(request.Phone)
			|| string.IsNullOrWhiteSpace(request.Gender)
			|| string.IsNullOrWhiteSpace(request.Email)
			|| string.IsNullOrWhiteSpace(request.HireDate))
		{
			return "员工建档缺少必要字段。";
		}

		if (request.Age <= 0)
		{
			return "员工年龄必须大于 0。";
		}

		var hasOrganizationId = !string.IsNullOrWhiteSpace(request.OrganizationId);
		var hasOrganizationName = !string.IsNullOrWhiteSpace(request.OrganizationName);
		if (hasOrganizationId != hasOrganizationName)
		{
			return "机构归属需要同时填写机构 id 和机构名称。";
		}

		if (request.EmploymentSource == "第三方合作" && string.IsNullOrWhiteSpace(request.PartnerAgencyName))
		{
			return "第三方合作人员必须填写合作机构名称。";
		}

		return null;
	}

	public static IReadOnlyList<AdminStaffScheduleItemResponse> CreateDefaultSchedule()
	{
		return
		[
			new AdminStaffScheduleItemResponse("周一", "待排班"),
			new AdminStaffScheduleItemResponse("周二", "待排班"),
			new AdminStaffScheduleItemResponse("周三", "待排班"),
			new AdminStaffScheduleItemResponse("周四", "待排班"),
			new AdminStaffScheduleItemResponse("周五", "待排班"),
			new AdminStaffScheduleItemResponse("周六", "待排班"),
			new AdminStaffScheduleItemResponse("周日", "待排班"),
		];
	}

	public static IReadOnlyList<AdminStaffScheduleItemResponse> DeserializeSchedule(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return CreateDefaultSchedule();
		}

		try
		{
			return JsonSerializer.Deserialize<List<AdminStaffScheduleItemResponse>>(value) ?? CreateDefaultSchedule();
		}
		catch
		{
			return CreateDefaultSchedule();
		}
	}

	public static IReadOnlyList<string> DeserializeCertificates(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return [];
		}

		try
		{
			return JsonSerializer.Deserialize<List<string>>(value) ?? [];
		}
		catch
		{
			return [];
		}
	}

	public static AdminStaffRecordResponse ToResponse(StaffMemberEntity entity)
	{
		return new AdminStaffRecordResponse(
			StaffId: entity.StaffId,
			TenantId: entity.TenantId,
			Name: entity.Name,
			Role: entity.Role,
			Department: entity.Department,
			OrganizationId: entity.OrganizationId,
			OrganizationName: entity.OrganizationName,
			EmploymentSource: entity.EmploymentSource,
			PartnerAgencyId: entity.PartnerAgencyId,
			PartnerAgencyName: entity.PartnerAgencyName,
			PartnerAffiliationRole: entity.PartnerAffiliationRole,
			Phone: entity.Phone,
			Status: entity.Status,
			Gender: entity.Gender,
			Email: entity.Email,
			Age: entity.Age,
			Performance: entity.Performance,
			Attendance: entity.Attendance,
			Satisfaction: entity.Satisfaction,
			HireDate: entity.HireDate,
			Schedule: DeserializeSchedule(entity.ScheduleJson),
			Certificates: DeserializeCertificates(entity.CertificatesJson),
			Bonus: entity.Bonus,
			LifecycleStatus: entity.LifecycleStatus,
			CreatedAt: entity.CreatedAtUtc.ToString("O"),
			ActivatedAt: entity.ActivatedAtUtc?.ToString("O"),
			OnboardingNote: entity.OnboardingNote);
	}
}