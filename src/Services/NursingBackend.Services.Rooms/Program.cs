using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.Services.Rooms;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<RoomsDbContext>(options =>
	options.UseNpgsql(PostgresConnectionStrings.Resolve(builder.Configuration, "RoomsPostgres", "nursing_rooms")));

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "rooms-service",
	ServiceType: "domain-service",
	BoundedContext: "rooms-and-capacity",
	Consumers: ["admin-bff", "elder-service"],
	Capabilities: ["room-master-data", "capacity-status", "room-activation"]));

app.MapGet("/api/rooms/rooms", async (
	HttpContext context,
	RoomsDbContext dbContext,
	CancellationToken cancellationToken,
	string? keyword,
	string? status,
	string? lifecycleStatus,
	string? organizationName,
	int? page,
	int? pageSize) =>
{
	var tenantId = GetTenantId(context);
	if (tenantId is null)
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var normalizedKeyword = keyword?.Trim();
	var normalizedOrganizationName = organizationName?.Trim();
	var currentPage = page is > 0 ? page.Value : 1;
	var currentPageSize = pageSize is > 0 and <= 200 ? pageSize.Value : 100;

	var query = dbContext.Rooms
		.AsNoTracking()
		.Where(item => item.TenantId == tenantId);

	if (!string.IsNullOrWhiteSpace(normalizedKeyword))
	{
		query = query.Where(item =>
			EF.Functions.ILike(item.RoomId, $"%{normalizedKeyword}%")
			|| EF.Functions.ILike(item.Name, $"%{normalizedKeyword}%")
			|| EF.Functions.ILike(item.OrganizationName, $"%{normalizedKeyword}%"));
	}

	if (!string.IsNullOrWhiteSpace(status))
	{
		query = query.Where(item => item.Status == status);
	}

	if (!string.IsNullOrWhiteSpace(lifecycleStatus))
	{
		query = query.Where(item => item.LifecycleStatus == lifecycleStatus);
	}

	if (!string.IsNullOrWhiteSpace(normalizedOrganizationName))
	{
		query = query.Where(item => EF.Functions.ILike(item.OrganizationName, $"%{normalizedOrganizationName}%"));
	}

	var total = await query.CountAsync(cancellationToken);
	var items = await query
		.OrderBy(item => item.LifecycleStatus == "待启用" ? 0 : item.Status == "维护中" ? 1 : item.CleanStatus != "已清洁" ? 2 : 3)
		.ThenBy(item => item.NextClean)
		.ThenBy(item => item.RoomId)
		.Skip((currentPage - 1) * currentPageSize)
		.Take(currentPageSize)
		.ToListAsync(cancellationToken);

	return Results.Ok(new AdminRoomListResponse(
		Items: items.Select(ToResponse).ToArray(),
		Total: total,
		Page: currentPage,
		PageSize: currentPageSize));
}).RequireAuthorization();

app.MapGet("/api/rooms/rooms/{roomId}", async (string roomId, HttpContext context, RoomsDbContext dbContext, CancellationToken cancellationToken) =>
{
	var tenantId = GetTenantId(context);
	if (tenantId is null)
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var entity = await dbContext.Rooms
		.AsNoTracking()
		.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.RoomId == roomId, cancellationToken);
	if (entity is null)
	{
		return Results.Problem(title: $"房间 {roomId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	return Results.Ok(ToResponse(entity));
}).RequireAuthorization();

app.MapPost("/api/rooms/rooms", async (HttpContext context, AdminRoomCreateRequest request, RoomsDbContext dbContext, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var validationError = ValidateCreateRequest(request);
	if (validationError is not null)
	{
		return Results.Problem(title: validationError, statusCode: StatusCodes.Status400BadRequest);
	}

	var normalizedRoomId = request.RoomId.Trim();
	var duplicate = await dbContext.Rooms.AnyAsync(item => item.TenantId == requestContext.TenantId && item.RoomId == normalizedRoomId, cancellationToken);
	if (duplicate)
	{
		return Results.Problem(title: "房间编号已存在。", statusCode: StatusCodes.Status409Conflict);
	}

	var now = DateTimeOffset.UtcNow;
	var entity = new RoomEntity
	{
		RoomId = normalizedRoomId,
		TenantId = requestContext.TenantId,
		Name = request.Name.Trim(),
		Floor = request.Floor,
		FloorName = FormatFloorName(request.Floor),
		Type = request.Type.Trim(),
		Capacity = request.Capacity,
		Status = "待启用",
		OrganizationId = string.IsNullOrWhiteSpace(request.OrganizationId) ? null : request.OrganizationId.Trim(),
		OrganizationName = request.OrganizationName.Trim(),
		FacilitiesJson = JsonSerializer.Serialize((request.Facilities ?? []).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct().ToArray()),
		CleanStatus = "待清洁",
		LastClean = "待首轮保洁",
		NextClean = "启用后生成",
		LifecycleStatus = "待启用",
		CreatedAtUtc = now,
	};

	await dbContext.Rooms.AddAsync(entity, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(ToResponse(entity));
}).RequireAuthorization();

app.MapPost("/api/rooms/rooms/{roomId}/activate", async (string roomId, HttpContext context, AdminRoomActivateRequest request, RoomsDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var entity = await dbContext.Rooms.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.RoomId == roomId, cancellationToken);
	if (entity is null)
	{
		return Results.Problem(title: $"房间 {roomId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	if (entity.LifecycleStatus == "已启用")
	{
		return Results.Problem(title: "该房间已启用。", statusCode: StatusCodes.Status409Conflict);
	}

	var activatedAt = timeProvider.GetUtcNow();
	entity.LifecycleStatus = "已启用";
	entity.Status = "可入住";
	entity.CleanStatus = "已清洁";
	entity.LastClean = FormatDateTime(activatedAt);
	entity.NextClean = $"{FormatDate(activatedAt.AddDays(1))} 07:00";
	entity.ActivatedAtUtc = activatedAt;
	entity.ActivationNote = string.IsNullOrWhiteSpace(request.ActivationNote)
		? $"{requestContext.UserName ?? "当前用户"} 已复核房间资料，允许进入排房资源池。"
		: request.ActivationNote.Trim();

	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(ToResponse(entity));
}).RequireAuthorization();

app.Run();

static string? GetTenantId(HttpContext context)
{
	var tenantId = context.GetPlatformRequestContext()?.TenantId;
	return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
}

static string? ValidateCreateRequest(AdminRoomCreateRequest request)
    => RoomServicePolicy.ValidateCreateRequest(request);

static string FormatFloorName(int floor) => RoomServicePolicy.FormatFloorName(floor);

static string FormatDate(DateTimeOffset value) => RoomServicePolicy.FormatDate(value);

static string FormatDateTime(DateTimeOffset value) => RoomServicePolicy.FormatDateTime(value);

static AdminRoomRecordResponse ToResponse(RoomEntity entity) => RoomServicePolicy.ToResponse(entity);