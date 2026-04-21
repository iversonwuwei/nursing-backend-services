using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.Services.Visit;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<VisitDbContext>(options =>
	options.UseNpgsql(PostgresConnectionStrings.Resolve(builder.Configuration, "VisitPostgres", "nursing_visit")));

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "visit-service",
	ServiceType: "domain-service",
	BoundedContext: "visit-collaboration",
	Consumers: ["family-bff", "admin-bff", "notification-service"],
	Capabilities: ["visit-request", "visit-approval", "visit-checkin", "video-session-metadata"]));

app.MapPost("/api/visits/appointments", async (HttpContext context, VisitAppointmentCreateRequest request, VisitDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var entity = new VisitAppointmentEntity
	{
		VisitId = $"VIS-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		ElderId = request.ElderId,
		VisitorName = request.VisitorName,
		Relation = request.Relation,
		Phone = request.Phone,
		PlannedAtUtc = request.PlannedAtUtc,
		VisitType = request.VisitType,
		Notes = request.Notes,
		Status = "Requested",
	};

	dbContext.VisitAppointments.Add(entity);
	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-VISIT-{entity.VisitId}",
		TenantId = requestContext.TenantId,
		AggregateType = "Visit",
		AggregateId = entity.VisitId,
		EventType = "VisitRequested",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { entity.VisitId, entity.ElderId, entity.VisitorName, entity.PlannedAtUtc }),
		CreatedAtUtc = DateTimeOffset.UtcNow,
	});
	await dbContext.SaveChangesAsync(cancellationToken);

	if (configuration.GetValue("Outbox:DispatchInlineOnWrite", false))
	{
		await VisitOutboxNotificationDispatcher.DispatchPendingAsync(
			dbContext,
			httpClientFactory.CreateClient(),
			context,
			configuration,
			cancellationToken,
			maxMessages: 1);
	}

	return Results.Ok(new VisitAppointmentResponse(
		VisitId: entity.VisitId,
		ElderId: entity.ElderId,
		TenantId: entity.TenantId,
		VisitorName: entity.VisitorName,
		Relation: entity.Relation,
		Status: entity.Status,
		PlannedAtUtc: entity.PlannedAtUtc));
}).RequireAuthorization();

app.MapGet("/api/visits/elders/{elderId}/appointments", async (string elderId, VisitDbContext dbContext) =>
{
	var items = await dbContext.VisitAppointments.Where(item => item.ElderId == elderId).OrderByDescending(item => item.PlannedAtUtc).ToListAsync();

	return Results.Ok(items.Select(entity => new VisitAppointmentResponse(
		VisitId: entity.VisitId,
		ElderId: entity.ElderId,
		TenantId: entity.TenantId,
		VisitorName: entity.VisitorName,
		Relation: entity.Relation,
		Status: entity.Status,
		PlannedAtUtc: entity.PlannedAtUtc)));
}).RequireAuthorization();

app.MapGet("/api/visits/appointments", async (HttpContext context, VisitDbContext dbContext, int? take, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var limit = Math.Clamp(take ?? 100, 1, 500);
	var tenantId = requestContext.TenantId;

	var items = await dbContext.VisitAppointments
		.Where(item => item.TenantId == tenantId)
		.OrderByDescending(item => item.PlannedAtUtc)
		.Take(limit)
		.ToListAsync(cancellationToken);

	return Results.Ok(items.Select(entity => new AdminVisitAppointmentResponse(
		VisitId: entity.VisitId,
		ElderId: entity.ElderId,
		TenantId: entity.TenantId,
		VisitorName: entity.VisitorName,
		Relation: entity.Relation,
		Phone: string.IsNullOrWhiteSpace(entity.Phone) ? null : entity.Phone,
		PlannedAtUtc: entity.PlannedAtUtc,
		VisitType: entity.VisitType,
		Status: entity.Status,
		Notes: string.IsNullOrWhiteSpace(entity.Notes) ? null : entity.Notes)));
}).RequireAuthorization();

app.MapPost("/api/visits/outbox/dispatch", async (HttpContext context, VisitDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	var dispatched = await VisitOutboxNotificationDispatcher.DispatchPendingAsync(
		dbContext,
		httpClientFactory.CreateClient(),
		context,
		configuration,
		cancellationToken);
	var pending = await dbContext.OutboxMessages.CountAsync(item => item.DispatchedAtUtc == null && item.EventType == "VisitRequested", cancellationToken);

	return Results.Ok(new
	{
		dispatched,
		pending,
		service = "visit-service",
		utc = DateTimeOffset.UtcNow,
	});
}).RequireAuthorization();

app.Run();
