using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.Services.Organization;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<OrganizationDbContext>(options =>
	options.UseNpgsql(PostgresConnectionStrings.Resolve(builder.Configuration, "OrganizationsPostgres", "nursing_organizations")));

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "organization-service",
	ServiceType: "domain-service",
	BoundedContext: "organization-topology",
	Consumers: ["admin-bff", "rooms-service"],
	Capabilities: ["organization-master-data", "organization-activation"]));

app.MapGet("/api/organizations/organizations", async (
	HttpContext context,
	OrganizationDbContext dbContext,
	CancellationToken cancellationToken,
	string? keyword,
	string? status,
	string? lifecycleStatus,
	int? page,
	int? pageSize) =>
{
	var tenantId = GetTenantId(context);
	if (tenantId is null)
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var normalizedKeyword = keyword?.Trim();
	var currentPage = page is > 0 ? page.Value : 1;
	var currentPageSize = pageSize is > 0 and <= 200 ? pageSize.Value : 100;

	var query = dbContext.Organizations
		.AsNoTracking()
		.Where(item => item.TenantId == tenantId);

	if (!string.IsNullOrWhiteSpace(normalizedKeyword))
	{
		query = query.Where(item =>
			EF.Functions.ILike(item.Name, $"%{normalizedKeyword}%")
			|| EF.Functions.ILike(item.Address, $"%{normalizedKeyword}%")
			|| EF.Functions.ILike(item.Manager, $"%{normalizedKeyword}%")
			|| EF.Functions.ILike(item.Phone, $"%{normalizedKeyword}%"));
	}

	if (!string.IsNullOrWhiteSpace(status))
	{
		query = query.Where(item => item.Status == status);
	}

	if (!string.IsNullOrWhiteSpace(lifecycleStatus))
	{
		query = query.Where(item => item.LifecycleStatus == lifecycleStatus);
	}

	var total = await query.CountAsync(cancellationToken);
	var items = await query
		.OrderBy(item => item.LifecycleStatus == "待启用" ? 0 : 1)
		.ThenBy(item => item.Status == "暂停营业" ? 1 : 0)
		.ThenBy(item => item.Name)
		.Skip((currentPage - 1) * currentPageSize)
		.Take(currentPageSize)
		.ToListAsync(cancellationToken);

	return Results.Ok(new OrganizationListResponse(
		Items: items.Select(ToResponse).ToArray(),
		Total: total,
		Page: currentPage,
		PageSize: currentPageSize));
}).RequireAuthorization();

app.MapGet("/api/organizations/organizations/{organizationId}", async (string organizationId, HttpContext context, OrganizationDbContext dbContext, CancellationToken cancellationToken) =>
{
	var tenantId = GetTenantId(context);
	if (tenantId is null)
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var entity = await dbContext.Organizations
		.AsNoTracking()
		.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.OrganizationId == organizationId, cancellationToken);
	if (entity is null)
	{
		return Results.Problem(title: $"机构 {organizationId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	return Results.Ok(ToResponse(entity));
}).RequireAuthorization();

app.MapPost("/api/organizations/organizations", async (HttpContext context, OrganizationCreateRequest request, OrganizationDbContext dbContext, CancellationToken cancellationToken) =>
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

	var normalizedName = request.Name.Trim();
	var duplicate = await dbContext.Organizations.AnyAsync(item => item.TenantId == requestContext.TenantId && item.Name == normalizedName, cancellationToken);
	if (duplicate)
	{
		return Results.Problem(title: "机构名称已存在。", statusCode: StatusCodes.Status409Conflict);
	}

	var now = DateTimeOffset.UtcNow;
	var entity = new OrganizationEntity
	{
		OrganizationId = $"ORG-{now.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		Name = normalizedName,
		Address = request.Address.Trim(),
		Phone = request.Phone.Trim(),
		Status = "筹备中",
		EstablishedDate = FormatDate(now),
		Manager = request.Manager.Trim(),
		ManagerPhone = request.ManagerPhone.Trim(),
		Description = string.IsNullOrWhiteSpace(request.Description) ? "待补充机构简介。" : request.Description.Trim(),
		LifecycleStatus = "待启用",
		CreatedAtUtc = now,
	};

	await dbContext.Organizations.AddAsync(entity, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(ToResponse(entity));
}).RequireAuthorization();

app.MapPost("/api/organizations/organizations/{organizationId}/activate", async (string organizationId, HttpContext context, OrganizationActivateRequest request, OrganizationDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var entity = await dbContext.Organizations.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.OrganizationId == organizationId, cancellationToken);
	if (entity is null)
	{
		return Results.Problem(title: $"机构 {organizationId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	if (entity.LifecycleStatus == "已启用")
	{
		return Results.Problem(title: "该机构已启用。", statusCode: StatusCodes.Status409Conflict);
	}

	var activatedAt = timeProvider.GetUtcNow();
	entity.LifecycleStatus = "已启用";
	entity.Status = "运营中";
	entity.ActivatedAtUtc = activatedAt;
	entity.ActivationNote = string.IsNullOrWhiteSpace(request.ActivationNote)
		? $"{requestContext.UserName ?? "当前用户"} 已复核机构资料，允许进入运营台账。"
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

static string? ValidateCreateRequest(OrganizationCreateRequest request)
    => OrganizationServicePolicy.ValidateCreateRequest(request);

static string FormatDate(DateTimeOffset value) => OrganizationServicePolicy.FormatDate(value);

static OrganizationRecordResponse ToResponse(OrganizationEntity entity) => OrganizationServicePolicy.ToResponse(entity);