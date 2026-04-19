namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record AdminRoomBedOccupantResponse(
	string ElderId,
	string Name,
	string CareLevel,
	string CheckIn);

public sealed record AdminRoomBedInfoResponse(
	int BedId,
	string Status,
	AdminRoomBedOccupantResponse? Elder);

public sealed record AdminRoomRecordResponse(
	string RoomId,
	string TenantId,
	string Name,
	int Floor,
	string FloorName,
	string Type,
	int Capacity,
	int Occupied,
	string Status,
	string? OrganizationId,
	string OrganizationName,
	IReadOnlyList<string> Facilities,
	string CleanStatus,
	string LastClean,
	string NextClean,
	string LifecycleStatus,
	string CreatedAt,
	string? ActivatedAt,
	string? ActivationNote,
	IReadOnlyList<AdminRoomBedInfoResponse> BedsInfo);

public sealed record AdminRoomListResponse(
	IReadOnlyList<AdminRoomRecordResponse> Items,
	int Total,
	int Page,
	int PageSize);

public sealed record AdminRoomCreateRequest(
	string RoomId,
	string Name,
	int Floor,
	string Type,
	int Capacity,
	string? OrganizationId,
	string OrganizationName,
	IReadOnlyList<string>? Facilities);

public sealed record AdminRoomActivateRequest(
	string? ActivationNote);