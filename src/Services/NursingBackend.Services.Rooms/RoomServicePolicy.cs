using System.Text.Json;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Rooms;

internal static class RoomServicePolicy
{
	public static string? ValidateCreateRequest(AdminRoomCreateRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.RoomId)
			|| string.IsNullOrWhiteSpace(request.Name)
			|| string.IsNullOrWhiteSpace(request.Type)
			|| string.IsNullOrWhiteSpace(request.OrganizationName))
		{
			return "房间建档缺少必要字段。";
		}

		if (request.Floor < 1 || request.Floor > 20)
		{
			return "房间楼层需在 1 到 20 之间。";
		}

		if (request.Capacity < 1 || request.Capacity > 8)
		{
			return "房间床位数需在 1 到 8 之间。";
		}

		return null;
	}

	public static string FormatFloorName(int floor) => $"{floor}楼";

	public static IReadOnlyList<string> DeserializeFacilities(string? value)
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

	public static string FormatDate(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd");

	public static string FormatDateTime(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

	public static AdminRoomRecordResponse ToResponse(RoomEntity entity) => new(
		RoomId: entity.RoomId,
		TenantId: entity.TenantId,
		Name: entity.Name,
		Floor: entity.Floor,
		FloorName: entity.FloorName,
		Type: entity.Type,
		Capacity: entity.Capacity,
		Occupied: 0,
		Status: entity.Status,
		OrganizationId: entity.OrganizationId,
		OrganizationName: entity.OrganizationName,
		Facilities: DeserializeFacilities(entity.FacilitiesJson),
		CleanStatus: entity.CleanStatus,
		LastClean: entity.LastClean,
		NextClean: entity.NextClean,
		LifecycleStatus: entity.LifecycleStatus,
		CreatedAt: FormatDate(entity.CreatedAtUtc),
		ActivatedAt: entity.ActivatedAtUtc is null ? null : FormatDateTime(entity.ActivatedAtUtc.Value),
		ActivationNote: entity.ActivationNote,
		BedsInfo: []);
}