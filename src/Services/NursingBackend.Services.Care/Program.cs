using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.Services.Care;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<CareDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing"));

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "care-service",
	ServiceType: "domain-service",
	BoundedContext: "care-orchestration",
	Consumers: ["admin-bff", "nani-bff", "notification-service", "ai-orchestration-service"],
	Capabilities: ["care-plan", "task-assignment", "care-execution", "handover"]));

app.MapPost("/api/care/plans/from-admission", async (HttpContext context, CarePlanCreateFromAdmissionRequest request, CareDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var generatedAtUtc = DateTimeOffset.UtcNow;
	var carePlan = new CarePlanEntity
	{
		CarePlanId = $"CP-{generatedAtUtc.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		ElderId = request.ElderId,
		ElderName = request.ElderName,
		PlanLevel = request.CareLevel,
		Status = "Generated",
		GeneratedAtUtc = generatedAtUtc,
	};

	var generatedTasks = BuildTasks(requestContext.TenantId, request.ElderId, request.CareLevel);
	var existingPlan = await dbContext.CarePlans.FirstOrDefaultAsync(item => item.ElderId == request.ElderId);
	if (existingPlan is not null)
	{
		dbContext.CareTasks.RemoveRange(dbContext.CareTasks.Where(item => item.ElderId == request.ElderId));
		dbContext.CarePlans.Remove(existingPlan);
	}
	dbContext.CarePlans.Add(carePlan);
	dbContext.CareTasks.AddRange(generatedTasks);
	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-CARE-{carePlan.CarePlanId}",
		TenantId = requestContext.TenantId,
		AggregateType = "CarePlan",
		AggregateId = carePlan.CarePlanId,
		EventType = "CarePlanGenerated",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { request.ElderId, request.ElderName, request.CareLevel, TaskCount = generatedTasks.Count }),
		CreatedAtUtc = generatedAtUtc,
	});
	await dbContext.SaveChangesAsync(cancellationToken);

	if (configuration.GetValue("Outbox:DispatchInlineOnWrite", false))
	{
		await CareOutboxNotificationDispatcher.DispatchPendingAsync(
			dbContext,
			httpClientFactory.CreateClient(),
			context,
			configuration,
			cancellationToken,
			maxMessages: 1);
	}

	return Results.Ok(new CarePlanResponse(
		CarePlanId: carePlan.CarePlanId,
		ElderId: carePlan.ElderId,
		TenantId: carePlan.TenantId,
		PlanLevel: carePlan.PlanLevel,
		Status: carePlan.Status,
		Tasks: generatedTasks.Select(ToTaskResponse).ToArray(),
		GeneratedAtUtc: carePlan.GeneratedAtUtc));
}).RequireAuthorization();

app.MapGet("/api/care/elders/{elderId}/task-feed", async (string elderId, CareDbContext dbContext) =>
{
	var plan = await dbContext.CarePlans.FirstOrDefaultAsync(item => item.ElderId == elderId);
	if (plan is null)
	{
		return Results.Problem(title: $"老人 {elderId} 的护理计划不存在。", statusCode: StatusCodes.Status404NotFound);
	}
	var tasks = await dbContext.CareTasks.Where(item => item.ElderId == elderId).OrderBy(item => item.DueAtLabel).ToListAsync();

	return Results.Ok(new NaniTaskFeedResponse(
		ElderId: plan.ElderId,
		ElderName: plan.ElderName,
		CareLevel: plan.PlanLevel,
		Tasks: tasks.Select(ToTaskResponse).ToArray()));
}).RequireAuthorization();

app.MapGet("/api/care/outbox/elders/{elderId}", async (string elderId, CareDbContext dbContext) =>
{
	var items = await dbContext.OutboxMessages.Where(item => item.AggregateId == elderId || item.PayloadJson.Contains(elderId)).OrderByDescending(item => item.CreatedAtUtc).ToListAsync();
	return Results.Ok(items);
}).RequireAuthorization();

app.MapPost("/api/care/outbox/dispatch", async (HttpContext context, CareDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	var dispatched = await CareOutboxNotificationDispatcher.DispatchPendingAsync(
		dbContext,
		httpClientFactory.CreateClient(),
		context,
		configuration,
		cancellationToken);
	var pending = await dbContext.OutboxMessages.CountAsync(item => item.DispatchedAtUtc == null && item.EventType == "CarePlanGenerated", cancellationToken);

	return Results.Ok(new
	{
		dispatched,
		pending,
		service = "care-service",
		utc = DateTimeOffset.UtcNow,
	});
}).RequireAuthorization();

app.Run();

static List<CareTaskEntity> BuildTasks(string tenantId, string elderId, string careLevel)
{
	(string Title, string AssigneeRole, string DueAtLabel)[] taskTemplates = careLevel switch
	{
		"全护理" => [
			("晨间生命体征巡查", "护士", "08:00"),
			("喂餐与服药确认", "护理员", "12:00"),
			("夜间翻身与睡眠观察", "护理员", "21:00")
		],
		"半自理" => [
			("血压与血糖复测", "护士", "09:00"),
			("康复训练协助", "护理员", "15:00")
		],
		_ => [
			("晨间状态回访", "护理员", "09:30"),
			("晚间安全巡查", "护理员", "20:30")
		]
	};

	return taskTemplates.Select((item, index) => new CareTaskEntity
	{
		TaskId = $"TASK-{elderId}-{index + 1}",
		TenantId = tenantId,
		ElderId = elderId,
		Title = item.Item1,
		AssigneeRole = item.Item2,
		DueAtLabel = item.Item3,
		Status = "Pending",
	}).ToList();
}

static CareTaskResponse ToTaskResponse(CareTaskEntity task)
{
	return new CareTaskResponse(
		TaskId: task.TaskId,
		ElderId: task.ElderId,
		Title: task.Title,
		AssigneeRole: task.AssigneeRole,
		DueAtLabel: task.DueAtLabel,
		Status: task.Status);
}
