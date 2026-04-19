using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.Services.Staffing;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<StaffingDbContext>(options =>
	options.UseNpgsql(PostgresConnectionStrings.Resolve(builder.Configuration, "StaffingPostgres", "nursing_staffing")));

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "staffing-service",
	ServiceType: "domain-service",
	BoundedContext: "staffing-and-shifts",
	Consumers: ["admin-bff", "nani-bff", "care-service"],
	Capabilities: ["staff-profile", "onboarding", "shift-scheduling", "workforce-assignment"]));

app.MapGet("/api/staffing/staff", async (
	HttpContext context,
	StaffingDbContext dbContext,
	CancellationToken cancellationToken,
	string? keyword,
	string? department,
	string? employmentSource,
	string? status,
	string? lifecycleStatus,
	string? organizationId,
	string? partnerAgency,
	int? page,
	int? pageSize) =>
{
	var tenantId = GetTenantId(context);
	if (tenantId is null)
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var normalizedKeyword = keyword?.Trim();
	var normalizedPartnerAgency = partnerAgency?.Trim();
	var currentPage = page is > 0 ? page.Value : 1;
	var currentPageSize = pageSize is > 0 and <= 200 ? pageSize.Value : 100;

	var query = dbContext.StaffMembers
		.AsNoTracking()
		.Where(item => item.TenantId == tenantId);

	if (!string.IsNullOrWhiteSpace(normalizedKeyword))
	{
		query = query.Where(item =>
			EF.Functions.ILike(item.Name, $"%{normalizedKeyword}%")
			|| EF.Functions.ILike(item.StaffId, $"%{normalizedKeyword}%")
			|| EF.Functions.ILike(item.Phone, $"%{normalizedKeyword}%")
			|| EF.Functions.ILike(item.Email, $"%{normalizedKeyword}%"));
	}

	if (!string.IsNullOrWhiteSpace(department))
	{
		query = query.Where(item => item.Department == department);
	}

	if (!string.IsNullOrWhiteSpace(employmentSource))
	{
		query = query.Where(item => item.EmploymentSource == employmentSource);
	}

	if (!string.IsNullOrWhiteSpace(status))
	{
		query = query.Where(item => item.Status == status);
	}

	if (!string.IsNullOrWhiteSpace(lifecycleStatus))
	{
		query = query.Where(item => item.LifecycleStatus == lifecycleStatus);
	}

	if (!string.IsNullOrWhiteSpace(organizationId))
	{
		var normalizedOrganizationId = organizationId.Trim();
		query = query.Where(item => item.OrganizationId == normalizedOrganizationId);
	}

	if (!string.IsNullOrWhiteSpace(normalizedPartnerAgency))
	{
		query = query.Where(item => item.PartnerAgencyName != null && EF.Functions.ILike(item.PartnerAgencyName, $"%{normalizedPartnerAgency}%"));
	}

	var total = await query.CountAsync(cancellationToken);
	var items = await query
		.OrderBy(item => item.LifecycleStatus == "待入职" ? 0 : item.Status == "休假" ? 1 : item.EmploymentSource == "第三方合作" ? 2 : 3)
		.ThenByDescending(item => item.CreatedAtUtc)
		.Skip((currentPage - 1) * currentPageSize)
		.Take(currentPageSize)
		.ToListAsync(cancellationToken);

	return Results.Ok(new AdminStaffListResponse(
		Items: items.Select(ToResponse).ToArray(),
		Total: total,
		Page: currentPage,
		PageSize: currentPageSize));
}).RequireAuthorization();

app.MapGet("/api/staffing/staff/{staffId}", async (string staffId, HttpContext context, StaffingDbContext dbContext, CancellationToken cancellationToken) =>
{
	var tenantId = GetTenantId(context);
	if (tenantId is null)
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var entity = await dbContext.StaffMembers
		.AsNoTracking()
		.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.StaffId == staffId, cancellationToken);
	if (entity is null)
	{
		return Results.Problem(title: $"员工 {staffId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	return Results.Ok(ToResponse(entity));
}).RequireAuthorization();

app.MapPost("/api/staffing/staff", async (HttpContext context, AdminStaffCreateRequest request, StaffingDbContext dbContext, CancellationToken cancellationToken) =>
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

	var normalizedPhone = request.Phone.Trim();
	var normalizedEmail = request.Email.Trim();
	var normalizedName = request.Name.Trim();
	var duplicate = await dbContext.StaffMembers.AnyAsync(item =>
		item.TenantId == requestContext.TenantId
		&& (item.Phone == normalizedPhone || item.Email == normalizedEmail)
		&& item.Name == normalizedName,
		cancellationToken);
	if (duplicate)
	{
		return Results.Problem(title: "已存在相同姓名且手机号或邮箱一致的员工主档。", statusCode: StatusCodes.Status409Conflict);
	}

	var now = DateTimeOffset.UtcNow;
	var entity = new StaffMemberEntity
	{
		StaffId = $"STF-{now.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		Name = normalizedName,
		Role = request.Role.Trim(),
		Department = request.Department.Trim(),
		OrganizationId = string.IsNullOrWhiteSpace(request.OrganizationId) ? null : request.OrganizationId.Trim(),
		OrganizationName = string.IsNullOrWhiteSpace(request.OrganizationName) ? null : request.OrganizationName.Trim(),
		EmploymentSource = request.EmploymentSource,
		PartnerAgencyId = string.IsNullOrWhiteSpace(request.PartnerAgencyId) ? null : request.PartnerAgencyId.Trim(),
		PartnerAgencyName = string.IsNullOrWhiteSpace(request.PartnerAgencyName) ? null : request.PartnerAgencyName.Trim(),
		PartnerAffiliationRole = string.IsNullOrWhiteSpace(request.PartnerAffiliationRole) ? null : request.PartnerAffiliationRole.Trim(),
		Phone = normalizedPhone,
		Status = "待入职",
		Gender = request.Gender,
		Email = normalizedEmail,
		Age = request.Age,
		Performance = 0,
		Attendance = 0,
		Satisfaction = 0,
		HireDate = request.HireDate.Trim(),
		ScheduleJson = JsonSerializer.Serialize(CreateDefaultSchedule()),
		CertificatesJson = "[]",
		Bonus = "待核定",
		LifecycleStatus = "待入职",
		CreatedAtUtc = now,
	};

	await dbContext.StaffMembers.AddAsync(entity, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(ToResponse(entity));
}).RequireAuthorization();

app.MapPost("/api/staffing/staff/{staffId}/activate", async (string staffId, HttpContext context, AdminStaffActivateRequest request, StaffingDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var entity = await dbContext.StaffMembers.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.StaffId == staffId, cancellationToken);
	if (entity is null)
	{
		return Results.Problem(title: $"员工 {staffId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	if (entity.LifecycleStatus == "已入职")
	{
		return Results.Problem(title: "该员工已确认入职。", statusCode: StatusCodes.Status409Conflict);
	}

	entity.LifecycleStatus = "已入职";
	entity.Status = "在职";
	entity.ActivatedAtUtc = timeProvider.GetUtcNow();
	entity.OnboardingNote = string.IsNullOrWhiteSpace(request.OnboardingNote)
		? $"{requestContext.UserName ?? "当前用户"} 已复核员工资料，允许纳入排班与任务台账。"
		: request.OnboardingNote.Trim();

	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(ToResponse(entity));
}).RequireAuthorization();

app.Run();

static string? GetTenantId(HttpContext context)
{
	var tenantId = context.GetPlatformRequestContext()?.TenantId;
	return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
}

static string? ValidateCreateRequest(AdminStaffCreateRequest request)
	=> StaffingServicePolicy.ValidateCreateRequest(request);

static IReadOnlyList<AdminStaffScheduleItemResponse> CreateDefaultSchedule()
	=> StaffingServicePolicy.CreateDefaultSchedule();

static AdminStaffRecordResponse ToResponse(StaffMemberEntity entity) => StaffingServicePolicy.ToResponse(entity);
