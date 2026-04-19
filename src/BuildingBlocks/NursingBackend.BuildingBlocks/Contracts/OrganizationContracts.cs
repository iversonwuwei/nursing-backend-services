namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record OrganizationRecordResponse(
	string OrganizationId,
	string TenantId,
	string Name,
	string Address,
	string Phone,
	string Status,
	string EstablishedDate,
	string Manager,
	string ManagerPhone,
	string Description,
	string LifecycleStatus,
	string CreatedAt,
	string? ActivatedAt,
	string? ActivationNote);

public sealed record OrganizationListResponse(
	IReadOnlyList<OrganizationRecordResponse> Items,
	int Total,
	int Page,
	int PageSize);

public sealed record OrganizationCreateRequest(
	string Name,
	string Address,
	string Phone,
	string Manager,
	string ManagerPhone,
	string Description);

public sealed record OrganizationActivateRequest(
	string? ActivationNote);

public sealed record AdminOrganizationRoomSummaryResponse(
	string RoomId,
	string Name,
	string FloorName,
	string Type,
	int Capacity,
	int Occupied,
	string Status,
	string CleanStatus);

public sealed record AdminOrganizationSummaryResponse(
	string OrganizationId,
	string TenantId,
	string Name,
	string Address,
	string Phone,
	string Status,
	string EstablishedDate,
	string Manager,
	string ManagerPhone,
	string Description,
	string LifecycleStatus,
	string CreatedAt,
	string? ActivatedAt,
	string? ActivationNote,
	int TotalBeds,
	int OccupiedBeds,
	int AvailableBeds,
	int ElderlyCount,
	int StaffCount,
	int RoomCount,
	string StaffIntegrationStatus);

public sealed record AdminOrganizationListResponse(
	IReadOnlyList<AdminOrganizationSummaryResponse> Items,
	int Total,
	int Page,
	int PageSize);

public sealed record AdminOrganizationDetailResponse(
	AdminOrganizationSummaryResponse Organization,
	IReadOnlyList<AdminOrganizationRoomSummaryResponse> Rooms,
	IReadOnlyList<AdminStaffRecordResponse> Staff);