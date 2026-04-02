using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.Services.Health;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<HealthDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing"));

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "health-service",
	ServiceType: "domain-service",
	BoundedContext: "health-observation",
	Consumers: ["admin-bff", "family-bff", "nani-bff", "operations-service", "ai-orchestration-service"],
	Capabilities: ["vitals-ingestion", "health-archive", "timeseries-observation", "risk-input"]));

app.MapPost("/api/health/archives/from-admission", async (HttpContext context, HealthArchiveCreateFromAdmissionRequest request, HealthDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var archive = new HealthArchiveEntity
	{
		ElderId = request.ElderId,
		TenantId = requestContext.TenantId,
		ElderName = request.ElderName,
		BloodPressure = request.BloodPressure,
		HeartRate = request.HeartRate,
		Temperature = request.Temperature,
		BloodSugar = request.BloodSugar,
		Oxygen = request.Oxygen,
		RiskSummary = string.IsNullOrWhiteSpace(request.AlertSummary) ? "需持续观察" : request.AlertSummary,
		UpdatedAtUtc = DateTimeOffset.UtcNow,
	};

	var existing = await dbContext.HealthArchives.FirstOrDefaultAsync(item => item.ElderId == request.ElderId);
	if (existing is null)
	{
		dbContext.HealthArchives.Add(archive);
	}
	else
	{
		existing.BloodPressure = archive.BloodPressure;
		existing.HeartRate = archive.HeartRate;
		existing.Temperature = archive.Temperature;
		existing.BloodSugar = archive.BloodSugar;
		existing.Oxygen = archive.Oxygen;
		existing.RiskSummary = archive.RiskSummary;
		existing.UpdatedAtUtc = archive.UpdatedAtUtc;
	}
	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-HEALTH-{request.ElderId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		AggregateType = "HealthArchive",
		AggregateId = request.ElderId,
		EventType = "HealthArchiveCreated",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { request.ElderId, request.ElderName, archive.RiskSummary }),
		CreatedAtUtc = archive.UpdatedAtUtc,
	});
	await dbContext.SaveChangesAsync();

	return Results.Ok(new HealthArchiveSummaryResponse(
		ElderId: archive.ElderId,
		TenantId: archive.TenantId,
		ElderName: archive.ElderName,
		BloodPressure: archive.BloodPressure,
		HeartRate: archive.HeartRate,
		Temperature: archive.Temperature,
		BloodSugar: archive.BloodSugar,
		Oxygen: archive.Oxygen,
		RiskSummary: archive.RiskSummary,
		UpdatedAtUtc: archive.UpdatedAtUtc));
}).RequireAuthorization();

app.MapGet("/api/health/elders/{elderId}/summary", async (string elderId, HealthDbContext dbContext) =>
{
	var archive = await dbContext.HealthArchives.FirstOrDefaultAsync(item => item.ElderId == elderId);
	if (archive is null)
	{
		return Results.Problem(title: $"老人 {elderId} 的健康档案不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	return Results.Ok(new HealthArchiveSummaryResponse(
		ElderId: archive.ElderId,
		TenantId: archive.TenantId,
		ElderName: archive.ElderName,
		BloodPressure: archive.BloodPressure,
		HeartRate: archive.HeartRate,
		Temperature: archive.Temperature,
		BloodSugar: archive.BloodSugar,
		Oxygen: archive.Oxygen,
		RiskSummary: archive.RiskSummary,
		UpdatedAtUtc: archive.UpdatedAtUtc));
}).RequireAuthorization();

app.Run();
