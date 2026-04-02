using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.Services.Elder;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<ElderDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing"));

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "elder-service",
	ServiceType: "domain-service",
	BoundedContext: "elder-management",
	Consumers: ["admin-bff", "family-bff", "care-service", "visit-service", "billing-service"],
	Capabilities: ["elder-registry", "admission-lifecycle", "family-binding", "resident-profile"]));

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
		FamilyContactName = request.FamilyContactName,
		FamilyContactPhone = request.FamilyContactPhone,
		MedicalAlerts = [..request.MedicalAlerts],
		AdmissionStatus = admission.Status,
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

	return Results.Ok(new ElderProfileSummaryResponse(
		ElderId: elder.ElderId,
		TenantId: elder.TenantId,
		ElderName: elder.ElderName,
		CareLevel: elder.CareLevel,
		RoomNumber: elder.RoomNumber,
		AdmissionStatus: elder.AdmissionStatus,
		FamilyContactName: elder.FamilyContactName,
		MedicalAlerts: elder.MedicalAlerts));
}).RequireAuthorization();

app.Run();
