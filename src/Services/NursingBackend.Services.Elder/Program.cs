using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.Services.Elder;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<ElderDbContext>(options =>
	options.UseNpgsql(PostgresConnectionStrings.Resolve(builder.Configuration, "ElderPostgres", "nursing_elder")));

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "elder-service",
	ServiceType: "domain-service",
	BoundedContext: "elder-management",
	Consumers: ["admin-bff", "family-bff", "care-service", "visit-service", "billing-service"],
	Capabilities: ["elder-registry", "admission-lifecycle", "family-binding", "resident-profile"]));

app.MapGet("/api/elders", async (HttpContext context, ElderDbContext dbContext, string? name, string? status, string? careLevel, int page = 1, int pageSize = 20) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var query = dbContext.Elders.Where(e => e.TenantId == requestContext.TenantId);

	if (!string.IsNullOrWhiteSpace(name))
		query = query.Where(e => e.ElderName.Contains(name));
	if (!string.IsNullOrWhiteSpace(status))
		query = query.Where(e => e.AdmissionStatus == status);
	if (!string.IsNullOrWhiteSpace(careLevel))
		query = query.Where(e => e.CareLevel == careLevel);

	var total = await query.CountAsync();

	var items = await query
		.OrderBy(e => e.ElderName)
		.Skip((page - 1) * pageSize)
		.Take(pageSize)
		.Select(e => new ElderListItemResponse(
			ElderId: e.ElderId,
			TenantId: e.TenantId,
			ElderName: e.ElderName,
			Age: e.Age,
			Gender: e.Gender,
			CareLevel: e.CareLevel,
			RoomNumber: e.RoomNumber,
			AdmissionStatus: e.AdmissionStatus,
			FamilyContactName: e.FamilyContactName))
		.ToListAsync();

	return Results.Ok(new ElderListResponse(Items: items, Total: total, Page: page, PageSize: pageSize));
}).RequireAuthorization();

app.MapPost("/api/elders/admissions", async (HttpContext context, AdmissionCreateRequest request, ElderDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var admissionId = $"ADM-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
	var elderId = $"ELD-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
	var createdAtUtc = DateTimeOffset.UtcNow;

	var admission = new AdmissionRecordEntity
	{
		AdmissionId = admissionId,
		TenantId = requestContext.TenantId,
		ElderId = elderId,
		AdmissionReference = request.AdmissionReference,
		Status = "AdmissionReviewed",
		CareLevel = request.CareLevel,
		RoomNumber = request.RoomNumber,
		CreatedAtUtc = createdAtUtc,
	};

	var elder = new ElderProfileEntity
	{
		ElderId = elderId,
		TenantId = requestContext.TenantId,
		ElderName = request.ElderName,
		Age = request.Age,
		Gender = request.Gender,
		CareLevel = request.CareLevel,
		RoomNumber = request.RoomNumber,
		IdentityCard = NormalizeOptionalText(request.IdentityCard),
		BirthDate = NormalizeOptionalText(request.BirthDate),
		ElderPhone = NormalizeOptionalText(request.ElderPhone),
		FamilyContactName = request.FamilyContactName,
		FamilyContactPhone = request.FamilyContactPhone,
		AdlScore = NormalizeAdlScore(request.AdlScore),
		CognitiveLevel = NormalizeOptionalText(request.CognitiveLevel),
		MedicalAlerts = NormalizeStringList(request.MedicalAlerts),
		AdmissionStatus = admission.Status,
		EntrustmentType = NormalizeOptionalText(request.EntrustmentType),
		EntrustmentOrganization = NormalizeOptionalText(request.EntrustmentOrganization),
		MonthlySubsidy = NormalizeMonthlySubsidy(request.MonthlySubsidy),
		ServiceItems = NormalizeStringList(request.ServiceItems),
		ServiceNotes = NormalizeOptionalText(request.ServiceNotes),
	};

	dbContext.Admissions.Add(admission);
	dbContext.Elders.Add(elder);
	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-{admissionId}",
		TenantId = requestContext.TenantId,
		AggregateType = "Admission",
		AggregateId = admissionId,
		EventType = "AdmissionReviewed",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { admissionId, elderId, request.ElderName, request.CareLevel }),
		CreatedAtUtc = createdAtUtc,
	});
	await dbContext.SaveChangesAsync();

	return Results.Ok(new AdmissionRecordResponse(
		AdmissionId: admission.AdmissionId,
		ElderId: admission.ElderId,
		TenantId: admission.TenantId,
		ElderName: elder.ElderName,
		CareLevel: admission.CareLevel,
		RoomNumber: admission.RoomNumber,
		Status: admission.Status,
		CreatedAtUtc: admission.CreatedAtUtc));
}).RequireAuthorization();

app.MapGet("/api/elders/{elderId}", async (string elderId, ElderDbContext dbContext) =>
{
	var elder = await dbContext.Elders.FirstOrDefaultAsync(item => item.ElderId == elderId);
	if (elder is null)
	{
		return Results.Problem(title: $"老人 {elderId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	return Results.Ok(MapProfileSummary(elder));
}).RequireAuthorization();

app.MapPut("/api/elders/{elderId}", async (HttpContext context, string elderId, ElderProfileUpdateRequest request, ElderDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	if (string.IsNullOrWhiteSpace(request.CareLevel) || string.IsNullOrWhiteSpace(request.RoomNumber))
	{
		return Results.Problem(title: "护理等级和房间号不能为空。", statusCode: StatusCodes.Status400BadRequest);
	}

	var elder = await dbContext.Elders.FirstOrDefaultAsync(item => item.ElderId == elderId && item.TenantId == requestContext.TenantId);
	if (elder is null)
	{
		return Results.Problem(title: $"老人 {elderId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	elder.CareLevel = request.CareLevel.Trim();
	elder.RoomNumber = request.RoomNumber.Trim();
	if (request.Age is > 0)
	{
		elder.Age = request.Age.Value;
	}

	var normalizedGender = NormalizeOptionalText(request.Gender);
	if (!string.IsNullOrWhiteSpace(normalizedGender))
	{
		elder.Gender = normalizedGender;
	}

	elder.IdentityCard = NormalizeOptionalText(request.IdentityCard);
	elder.BirthDate = NormalizeOptionalText(request.BirthDate);
	elder.ElderPhone = NormalizeOptionalText(request.ElderPhone);
	elder.FamilyContactName = request.FamilyContactName.Trim();
	elder.FamilyContactPhone = request.FamilyContactPhone.Trim();
	elder.AdlScore = NormalizeAdlScore(request.AdlScore);
	elder.CognitiveLevel = NormalizeOptionalText(request.CognitiveLevel);
	elder.MedicalAlerts = NormalizeStringList(request.MedicalAlerts);
	elder.EntrustmentType = NormalizeOptionalText(request.EntrustmentType);
	elder.EntrustmentOrganization = NormalizeOptionalText(request.EntrustmentOrganization);
	elder.MonthlySubsidy = NormalizeMonthlySubsidy(request.MonthlySubsidy);
	elder.ServiceItems = NormalizeStringList(request.ServiceItems);
	elder.ServiceNotes = NormalizeOptionalText(request.ServiceNotes);

	var updatedAtUtc = DateTimeOffset.UtcNow;
	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-ELDER-{elderId}-{updatedAtUtc.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		AggregateType = "Elder",
		AggregateId = elderId,
		EventType = "ElderProfileUpdated",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
		{
			elderId,
			elder.CareLevel,
			elder.RoomNumber,
			elder.EntrustmentType,
			elder.MonthlySubsidy,
			updatedAtUtc,
		}),
		CreatedAtUtc = updatedAtUtc,
	});

	await dbContext.SaveChangesAsync();

	return Results.Ok(MapProfileSummary(elder));
}).RequireAuthorization();

app.Run();

static ElderProfileSummaryResponse MapProfileSummary(ElderProfileEntity elder)
{
	return new ElderProfileSummaryResponse(
		ElderId: elder.ElderId,
		TenantId: elder.TenantId,
		ElderName: elder.ElderName,
		Age: elder.Age,
		Gender: elder.Gender,
		CareLevel: elder.CareLevel,
		RoomNumber: elder.RoomNumber,
		AdmissionStatus: elder.AdmissionStatus,
		IdentityCard: elder.IdentityCard,
		BirthDate: elder.BirthDate,
		ElderPhone: elder.ElderPhone,
		FamilyContactName: elder.FamilyContactName,
		FamilyContactPhone: elder.FamilyContactPhone,
		AdlScore: elder.AdlScore,
		CognitiveLevel: elder.CognitiveLevel,
		MedicalAlerts: elder.MedicalAlerts,
		EntrustmentType: elder.EntrustmentType,
		EntrustmentOrganization: elder.EntrustmentOrganization,
		MonthlySubsidy: elder.MonthlySubsidy,
		ServiceItems: elder.ServiceItems,
		ServiceNotes: elder.ServiceNotes);
}

static List<string> NormalizeStringList(IEnumerable<string>? values)
{
	if (values is null)
	{
		return [];
	}

	return values
		.Select(value => value.Trim())
		.Where(value => !string.IsNullOrWhiteSpace(value))
		.Distinct(StringComparer.Ordinal)
		.ToList();
}

static string? NormalizeOptionalText(string? value)
{
	var trimmed = value?.Trim();
	return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
}

static decimal? NormalizeMonthlySubsidy(decimal? value)
{
	return value is > 0 ? Math.Round(value.Value, 2) : null;
}

static int? NormalizeAdlScore(int? value)
{
	return value is >= 0 and <= 100 ? value : null;
}
