using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Organization;

internal static class OrganizationServicePolicy
{
	public static string? ValidateCreateRequest(OrganizationCreateRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Name)
			|| string.IsNullOrWhiteSpace(request.Address)
			|| string.IsNullOrWhiteSpace(request.Phone)
			|| string.IsNullOrWhiteSpace(request.Manager)
			|| string.IsNullOrWhiteSpace(request.ManagerPhone))
		{
			return "机构建档缺少必要字段。";
		}

		if (request.Phone.Trim().Count(char.IsDigit) < 10)
		{
			return "机构联系电话格式无效。";
		}

		if (request.ManagerPhone.Trim().Count(char.IsDigit) < 11)
		{
			return "负责人电话至少填写 11 位有效手机号。";
		}

		if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > 1000)
		{
			return "机构简介长度不能超过 1000 个字符。";
		}

		return null;
	}

	public static string FormatDate(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd");

	public static string FormatDateTime(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

	public static OrganizationRecordResponse ToResponse(OrganizationEntity entity) => new(
		OrganizationId: entity.OrganizationId,
		TenantId: entity.TenantId,
		Name: entity.Name,
		Address: entity.Address,
		Phone: entity.Phone,
		Status: entity.Status,
		EstablishedDate: entity.EstablishedDate,
		Manager: entity.Manager,
		ManagerPhone: entity.ManagerPhone,
		Description: entity.Description,
		LifecycleStatus: entity.LifecycleStatus,
		CreatedAt: FormatDate(entity.CreatedAtUtc),
		ActivatedAt: entity.ActivatedAtUtc is null ? null : FormatDateTime(entity.ActivatedAtUtc.Value),
		ActivationNote: entity.ActivationNote);
}