using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.Services.Care;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddSingleton<CareWorkflowTelemetry>();
builder.Services.AddDbContext<CareDbContext>(options =>
	options.UseNpgsql(PostgresConnectionStrings.Resolve(builder.Configuration, "CarePostgres", "nursing_care")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<CareDbContext>();
	await dbContext.Database.EnsureCreatedAsync();
}

app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "care-service",
	ServiceType: "domain-service",
	BoundedContext: "care-orchestration",
	Consumers: ["admin-bff", "nani-bff", "notification-service", "ai-orchestration-service"],
	Capabilities: ["care-plan", "task-assignment", "care-execution", "handover", "service-package", "service-plan-board", "schedule-assignment"]));

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
	var existingPlan = await dbContext.CarePlans.FirstOrDefaultAsync(item => item.ElderId == request.ElderId, cancellationToken);
	if (existingPlan is not null)
	{
		dbContext.CareTasks.RemoveRange(dbContext.CareTasks.Where(item => item.ElderId == request.ElderId));
		dbContext.CarePlans.Remove(existingPlan);
	}
	await dbContext.CarePlans.AddAsync(carePlan, cancellationToken);
	await dbContext.CareTasks.AddRangeAsync(generatedTasks, cancellationToken);
	await dbContext.OutboxMessages.AddAsync(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-CARE-{carePlan.CarePlanId}",
		TenantId = requestContext.TenantId,
		AggregateType = "CarePlan",
		AggregateId = carePlan.CarePlanId,
		EventType = "CarePlanGenerated",
		PayloadJson = JsonSerializer.Serialize(new { request.ElderId, request.ElderName, request.CareLevel, TaskCount = generatedTasks.Count }),
		CreatedAtUtc = generatedAtUtc,
	}, cancellationToken);
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

app.MapGet("/api/care/elders/{elderId}/task-feed", async (string elderId, CareDbContext dbContext, CancellationToken cancellationToken) =>
{
	var plan = await dbContext.CarePlans.FirstOrDefaultAsync(item => item.ElderId == elderId, cancellationToken);
	if (plan is null)
	{
		return Results.Problem(title: $"老人 {elderId} 的护理计划不存在。", statusCode: StatusCodes.Status404NotFound);
	}
	var tasks = await dbContext.CareTasks.Where(item => item.ElderId == elderId).OrderBy(item => item.DueAtLabel).ToListAsync(cancellationToken);

	return Results.Ok(new NaniTaskFeedResponse(
		ElderId: plan.ElderId,
		ElderName: plan.ElderName,
		CareLevel: plan.PlanLevel,
		Tasks: tasks.Select(ToTaskResponse).ToArray()));
}).RequireAuthorization();

app.MapGet("/api/care/outbox/elders/{elderId}", async (string elderId, CareDbContext dbContext, CancellationToken cancellationToken) =>
{
	var items = await dbContext.OutboxMessages.Where(item => item.AggregateId == elderId || item.PayloadJson.Contains(elderId)).OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
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

app.MapGet("/api/care/admin/workflow-board", async (HttpContext context, CareDbContext dbContext, CareWorkflowTelemetry telemetry, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	await EnsureWorkflowSeedDataAsync(dbContext, requestContext.TenantId, cancellationToken);
	var board = await BuildWorkflowBoardAsync(dbContext, telemetry, requestContext.TenantId, cancellationToken);
	return Results.Ok(board);
}).RequireAuthorization();

app.MapGet("/api/care/admin/observability", async (HttpContext context, CareDbContext dbContext, CareWorkflowTelemetry telemetry, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	await EnsureWorkflowSeedDataAsync(dbContext, requestContext.TenantId, cancellationToken);
	return Results.Ok(await BuildObservabilityAsync(dbContext, telemetry, requestContext.TenantId, cancellationToken));
}).RequireAuthorization();

app.MapGet("/api/care/admin/audits", async (HttpContext context, CareDbContext dbContext, int? take, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var size = take is > 0 and <= 200 ? take.Value : 50;
	var items = await dbContext.CareWorkflowAudits
		.Where(item => item.TenantId == requestContext.TenantId)
		.OrderByDescending(item => item.CreatedAtUtc)
		.Take(size)
		.ToListAsync(cancellationToken);

	return Results.Ok(items.Select(ToAuditResponse).ToArray());
}).RequireAuthorization();

app.MapPost("/api/care/admin/packages", async (HttpContext context, CreateServicePackageRequest request, CareDbContext dbContext, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}
	if (string.IsNullOrWhiteSpace(request.Name)
		|| string.IsNullOrWhiteSpace(request.CareLevel)
		|| string.IsNullOrWhiteSpace(request.TargetGroup)
		|| string.IsNullOrWhiteSpace(request.MonthlyPrice)
		|| request.ServiceScope.Count == 0)
	{
		return Results.Problem(title: "请先补齐套餐名称、护理等级、适用对象、月费和至少一个服务项。", statusCode: StatusCodes.Status400BadRequest);
	}

	var now = DateTimeOffset.UtcNow;
	var entity = new ServicePackageEntity
	{
		PackageId = $"PKG-{now.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		Name = request.Name.Trim(),
		CareLevel = request.CareLevel.Trim(),
		TargetGroup = request.TargetGroup.Trim(),
		MonthlyPrice = request.MonthlyPrice.Trim(),
		SettlementCycle = request.SettlementCycle.Trim(),
		ServiceScopeJson = SerializeList(request.ServiceScope),
		AddOnsJson = SerializeList(request.AddOns),
		BoundElders = 0,
		Status = "草稿",
		CreatedAtUtc = now,
		PricingNote = "套餐草稿已创建，待提交定价复核。",
	};

	await dbContext.ServicePackages.AddAsync(entity, cancellationToken);
	await AppendWorkflowAuditAsync(dbContext, requestContext, "ServicePackage", entity.PackageId, "CreateDraft", new { entity.Name, entity.CareLevel }, now, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(ToServicePackageResponse(entity));
}).RequireAuthorization();

app.MapPost("/api/care/admin/packages/{packageId}/actions/{action}", async (HttpContext context, string packageId, string action, CareDbContext dbContext, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var entity = await dbContext.ServicePackages.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.PackageId == packageId, cancellationToken);
	if (entity is null)
	{
		return Results.Problem(title: "未找到服务套餐。", statusCode: StatusCodes.Status404NotFound);
	}

	var now = DateTimeOffset.UtcNow;
	ServicePackageActionResponse response = action switch
	{
		"submit-pricing" => UpdatePackageState(entity, "待定价", "已提交财务复核，等待确认价格与结算周期。", "SubmitPricing"),
		"complete-pricing" => UpdatePackageState(entity, "待发布", "价格复核完成，等待护理主管发布。", "CompletePricing"),
		"publish" => PublishPackage(entity, now),
		"retire" => UpdatePackageState(entity, "已下线", "套餐已下线，仅保留历史追踪。", "Retire"),
		_ => new ServicePackageActionResponse(entity.PackageId, entity.Status, string.Empty),
	};

	if (string.IsNullOrWhiteSpace(response.Message))
	{
		return Results.Problem(title: "不支持的套餐动作。", statusCode: StatusCodes.Status400BadRequest);
	}

	await AppendWorkflowAuditAsync(dbContext, requestContext, "ServicePackage", entity.PackageId, response.Message, new { response.Status }, now, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(response);
}).RequireAuthorization();

app.MapPost("/api/care/admin/packages/{packageId}/plans", async (HttpContext context, string packageId, CareDbContext dbContext, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var packageEntity = await dbContext.ServicePackages.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.PackageId == packageId, cancellationToken);
	if (packageEntity is null)
	{
		return Results.Problem(title: "未找到来源套餐。", statusCode: StatusCodes.Status404NotFound);
	}

	var now = DateTimeOffset.UtcNow;
	var focusPrefix = string.Join(" + ", DeserializeList(packageEntity.ServiceScopeJson).Take(2));
	var plan = new ServicePlanEntity
	{
		PlanId = $"PLAN-{now.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		PackageId = packageEntity.PackageId,
		PackageName = packageEntity.Name,
		ElderlyName = "待绑定老人",
		Room = "待分配房间",
		CareLevel = packageEntity.CareLevel,
		Focus = string.IsNullOrWhiteSpace(focusPrefix) ? "执行计划" : $"{focusPrefix} 执行计划",
		ShiftSummary = packageEntity.CareLevel == "专项康复" ? "中班" : "早班 / 中班",
		OwnerRole = packageEntity.CareLevel == "专项康复" ? "康复师" : "护工",
		OwnerName = "待分派",
		RiskTagsJson = packageEntity.CareLevel == "全护" ? SerializeList(["高频照护"]) : SerializeList(Array.Empty<string>()),
		Source = "套餐生成",
		Status = "待复核",
		CreatedAtUtc = now,
		ReviewNote = "计划草稿已生成，等待护理主管复核。",
	};

	await dbContext.ServicePlans.AddAsync(plan, cancellationToken);
	await AppendWorkflowAuditAsync(dbContext, requestContext, "ServicePlan", plan.PlanId, "CreateFromPackage", new { plan.PackageId, plan.PackageName }, now, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(ToServicePlanResponse(plan));
}).RequireAuthorization();

app.MapPost("/api/care/admin/plans", async (HttpContext context, CreateServicePlanRequest request, CareDbContext dbContext, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}
	if (string.IsNullOrWhiteSpace(request.PackageId)
		|| string.IsNullOrWhiteSpace(request.ElderlyName)
		|| string.IsNullOrWhiteSpace(request.Room)
		|| string.IsNullOrWhiteSpace(request.Focus)
		|| string.IsNullOrWhiteSpace(request.Shift)
		|| string.IsNullOrWhiteSpace(request.OwnerRole)
		|| string.IsNullOrWhiteSpace(request.OwnerName))
	{
		return Results.Problem(title: "请先补齐来源套餐、老人、房间、计划重点、班次、责任角色和责任人。", statusCode: StatusCodes.Status400BadRequest);
	}

	var packageEntity = await dbContext.ServicePackages.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.PackageId == request.PackageId, cancellationToken);
	if (packageEntity is null)
	{
		return Results.Problem(title: "未找到来源套餐。", statusCode: StatusCodes.Status404NotFound);
	}

	var now = DateTimeOffset.UtcNow;
	var plan = new ServicePlanEntity
	{
		PlanId = $"PLAN-{now.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		PackageId = packageEntity.PackageId,
		PackageName = packageEntity.Name,
		ElderlyName = request.ElderlyName.Trim(),
		Room = request.Room.Trim(),
		CareLevel = packageEntity.CareLevel,
		Focus = request.Focus.Trim(),
		ShiftSummary = request.Shift.Trim(),
		OwnerRole = request.OwnerRole.Trim(),
		OwnerName = request.OwnerName.Trim(),
		RiskTagsJson = SerializeList(request.RiskTags),
		Source = request.Source.Trim(),
		Status = "待复核",
		CreatedAtUtc = now,
		ReviewNote = "计划草稿已生成，等待护理主管复核。",
	};

	await dbContext.ServicePlans.AddAsync(plan, cancellationToken);
	await AppendWorkflowAuditAsync(dbContext, requestContext, "ServicePlan", plan.PlanId, "CreateDraft", new { plan.ElderlyName, plan.OwnerName }, now, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(ToServicePlanResponse(plan));
}).RequireAuthorization();

app.MapPost("/api/care/admin/plans/{planId}/actions/{action}", async (HttpContext context, string planId, string action, CareDbContext dbContext, CareWorkflowTelemetry telemetry, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var plan = await dbContext.ServicePlans.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.PlanId == planId, cancellationToken);
	if (plan is null)
	{
		return Results.Problem(title: "未找到服务计划。", statusCode: StatusCodes.Status404NotFound);
	}

	var now = DateTimeOffset.UtcNow;
	ServicePlanActionResponse response = action switch
	{
		"review" => ReviewPlan(plan),
		"mark-exception" => MarkPlanException(plan),
		"archive" => ArchivePlan(plan),
		_ => new ServicePlanActionResponse(plan.PlanId, plan.Status, string.Empty),
	};

	if (string.IsNullOrWhiteSpace(response.Message))
	{
		return Results.Problem(title: "不支持的计划动作。", statusCode: StatusCodes.Status400BadRequest);
	}

	if (action == "archive")
	{
		telemetry.RecordPlanArchived(requestContext.TenantId, plan.PlanId);
	}

	await SyncAssignmentsForPlanAsync(dbContext, plan, now, cancellationToken);
	await AppendWorkflowAuditAsync(dbContext, requestContext, "ServicePlan", plan.PlanId, response.Message, new { response.Status, plan.OwnerName }, now, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(response);
}).RequireAuthorization();

app.MapPost("/api/care/admin/tasks/{taskId}/start", async (HttpContext context, string taskId, ServicePlanTaskActionRequest request, CareDbContext dbContext, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var planId = GetPlanIdFromTaskId(taskId);
	var plan = await dbContext.ServicePlans.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.PlanId == planId, cancellationToken);
	if (plan is null)
	{
		return Results.Problem(title: "未找到任务对应的服务计划。", statusCode: StatusCodes.Status404NotFound);
	}

	var now = DateTimeOffset.UtcNow;
	var execution = await dbContext.ServicePlanTaskExecutions.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.TaskExecutionId == taskId, cancellationToken);
	if (execution is null)
	{
		execution = new ServicePlanTaskExecutionEntity
		{
			TaskExecutionId = taskId,
			TenantId = requestContext.TenantId,
			PlanId = planId,
			Status = "执行中",
			HandledBy = request.HandledBy ?? requestContext.UserName,
			HandledAtUtc = now,
			ActionNote = request.ActionNote ?? "已接收服务计划，进入执行。",
			CreatedAtUtc = now,
			UpdatedAtUtc = now,
		};
		await dbContext.ServicePlanTaskExecutions.AddAsync(execution, cancellationToken);
	}
	else
	{
		execution.Status = "执行中";
		execution.HandledBy = request.HandledBy ?? execution.HandledBy ?? requestContext.UserName;
		execution.HandledAtUtc = now;
		execution.ActionNote = request.ActionNote ?? execution.ActionNote ?? "已接收服务计划，进入执行。";
		execution.UpdatedAtUtc = now;
	}

	await AppendWorkflowAuditAsync(dbContext, requestContext, "ServiceTask", taskId, "StartExecution", new { planId, execution.HandledBy }, now, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(new ServicePlanActionResponse(planId, "执行中", "任务已进入执行中。"));
}).RequireAuthorization();

app.MapPost("/api/care/admin/tasks/{taskId}/complete", async (HttpContext context, string taskId, ServicePlanTaskActionRequest request, CareDbContext dbContext, CareWorkflowTelemetry telemetry, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var planId = GetPlanIdFromTaskId(taskId);
	var plan = await dbContext.ServicePlans.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.PlanId == planId, cancellationToken);
	if (plan is null)
	{
		return Results.Problem(title: "未找到任务对应的服务计划。", statusCode: StatusCodes.Status404NotFound);
	}

	var now = DateTimeOffset.UtcNow;
	var execution = await dbContext.ServicePlanTaskExecutions.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.TaskExecutionId == taskId, cancellationToken);
	if (execution is null)
	{
		execution = new ServicePlanTaskExecutionEntity
		{
			TaskExecutionId = taskId,
			TenantId = requestContext.TenantId,
			PlanId = planId,
			Status = "已完成",
			HandledBy = request.HandledBy ?? requestContext.UserName,
			HandledAtUtc = now,
			ActionNote = request.ActionNote ?? "服务计划已执行完毕并归档。",
			CreatedAtUtc = now,
			UpdatedAtUtc = now,
		};
		await dbContext.ServicePlanTaskExecutions.AddAsync(execution, cancellationToken);
	}
	else
	{
		execution.Status = "已完成";
		execution.HandledBy = request.HandledBy ?? execution.HandledBy ?? requestContext.UserName;
		execution.HandledAtUtc = now;
		execution.ActionNote = request.ActionNote ?? execution.ActionNote ?? "服务计划已执行完毕并归档。";
		execution.UpdatedAtUtc = now;
	}

	plan.Status = "已归档";
	plan.ReviewNote = "任务中心确认执行完成并归档。";
	await SyncAssignmentsForPlanAsync(dbContext, plan, now, cancellationToken);
	await AppendWorkflowAuditAsync(dbContext, requestContext, "ServiceTask", taskId, "CompleteExecution", new { planId, execution.HandledBy }, now, cancellationToken);
	telemetry.RecordTaskCompleted(requestContext.TenantId, planId);
	telemetry.RecordPlanArchived(requestContext.TenantId, planId);
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(new ServicePlanActionResponse(planId, "已归档", "任务已完成，计划同步归档。"));
}).RequireAuthorization();

app.MapPut("/api/care/admin/tasks/{taskId}/note", async (HttpContext context, string taskId, SaveServicePlanTaskNoteRequest request, CareDbContext dbContext, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var planId = GetPlanIdFromTaskId(taskId);
	var now = DateTimeOffset.UtcNow;
	var execution = await dbContext.ServicePlanTaskExecutions.FirstOrDefaultAsync(item => item.TenantId == requestContext.TenantId && item.TaskExecutionId == taskId, cancellationToken);
	if (execution is null)
	{
		execution = new ServicePlanTaskExecutionEntity
		{
			TaskExecutionId = taskId,
			TenantId = requestContext.TenantId,
			PlanId = planId,
			Status = request.Status,
			HandledBy = request.HandledBy,
			HandledAtUtc = ParseTimestamp(request.HandledAtIso) ?? ParseDisplayTimestamp(request.HandledAt),
			ActionNote = request.ActionNote,
			CreatedAtUtc = now,
			UpdatedAtUtc = now,
		};
		await dbContext.ServicePlanTaskExecutions.AddAsync(execution, cancellationToken);
	}
	else
	{
		execution.Status = request.Status;
		execution.HandledBy = execution.HandledBy ?? request.HandledBy;
		execution.HandledAtUtc = execution.HandledAtUtc ?? ParseTimestamp(request.HandledAtIso) ?? ParseDisplayTimestamp(request.HandledAt);
		execution.ActionNote = request.ActionNote;
		execution.UpdatedAtUtc = now;
	}

	await AppendWorkflowAuditAsync(dbContext, requestContext, "ServiceTask", taskId, "SaveNote", new { request.Status }, now, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);
	return Results.Ok(new ServicePlanActionResponse(planId, request.Status, "任务备注已保存。"));
}).RequireAuthorization();

app.Run();

static async Task<NursingWorkflowBoardResponse> BuildWorkflowBoardAsync(CareDbContext dbContext, CareWorkflowTelemetry telemetry, string tenantId, CancellationToken cancellationToken)
{
	var packages = await dbContext.ServicePackages.Where(item => item.TenantId == tenantId).OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
	var plans = await dbContext.ServicePlans.Where(item => item.TenantId == tenantId).OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
	var taskExecutions = await dbContext.ServicePlanTaskExecutions.Where(item => item.TenantId == tenantId).ToListAsync(cancellationToken);
	var assignments = await dbContext.ServicePlanAssignments.Where(item => item.TenantId == tenantId).ToListAsync(cancellationToken);
	var schedule = BuildScheduleBoard(plans, assignments);
	telemetry.UpdateUnassignedBacklog(schedule.UnassignedPlans);

	return new NursingWorkflowBoardResponse(
		Packages: packages.Select(ToServicePackageResponse).ToArray(),
		Plans: plans.Select(ToServicePlanResponse).ToArray(),
		Tasks: BuildTaskResponses(plans, taskExecutions),
		Schedule: schedule,
		Observability: await BuildObservabilityAsync(dbContext, telemetry, tenantId, cancellationToken));
}

static async Task<CareWorkflowObservabilityResponse> BuildObservabilityAsync(CareDbContext dbContext, CareWorkflowTelemetry telemetry, string tenantId, CancellationToken cancellationToken)
{
	var pendingReviewPlans = await dbContext.ServicePlans.CountAsync(item => item.TenantId == tenantId && item.Status == "待复核", cancellationToken);
	var archivedPlans = await dbContext.ServicePlans.CountAsync(item => item.TenantId == tenantId && item.Status == "已归档", cancellationToken);
	var completedTasks = await dbContext.ServicePlanTaskExecutions.CountAsync(item => item.TenantId == tenantId && item.Status == "已完成", cancellationToken);
	var auditRecords = await dbContext.CareWorkflowAudits.CountAsync(item => item.TenantId == tenantId, cancellationToken);
	var unassignedPlans = await dbContext.ServicePlans.CountAsync(item => item.TenantId == tenantId && item.Status != "已归档" && item.Status != "待复核" && (item.OwnerName == "待分派" || item.OwnerName == string.Empty), cancellationToken);
	telemetry.UpdateUnassignedBacklog(unassignedPlans);

	return new CareWorkflowObservabilityResponse(
		PendingReviewPlans: pendingReviewPlans,
		UnassignedPlans: unassignedPlans,
		ArchivedPlans: archivedPlans,
		CompletedTasks: completedTasks,
		AuditRecords: auditRecords,
		TaskCompletionTotal: telemetry.TaskCompletionTotal,
		PlanArchiveTotal: telemetry.PlanArchiveTotal,
		UnassignedBacklogGauge: telemetry.UnassignedBacklogGauge);
}

static async Task EnsureWorkflowSeedDataAsync(CareDbContext dbContext, string tenantId, CancellationToken cancellationToken)
{
	if (await dbContext.ServicePackages.AnyAsync(item => item.TenantId == tenantId, cancellationToken))
	{
		return;
	}

	var now = DateTimeOffset.UtcNow;
	var packages = new[]
	{
		new ServicePackageEntity
		{
			PackageId = "PKG001",
			TenantId = tenantId,
			Name = "半自理标准包",
			CareLevel = "半自理",
			TargetGroup = "餐食协助、基础巡房和日间陪护老人",
			MonthlyPrice = "¥3,200",
			SettlementCycle = "月付",
			ServiceScopeJson = SerializeList(["用药提醒", "协助就餐", "基础巡房"]),
			AddOnsJson = SerializeList(["探视陪同", "夜间安睡观察"]),
			BoundElders = 42,
			Status = "已生效",
			CreatedAtUtc = now.AddDays(-30),
			PublishedAtUtc = now.AddDays(-26),
			PricingNote = "已完成财务与运营联合复核。",
		},
		new ServicePackageEntity
		{
			PackageId = "PKG002",
			TenantId = tenantId,
			Name = "全护照护包",
			CareLevel = "全护",
			TargetGroup = "失能或高频照护老人",
			MonthlyPrice = "¥5,800",
			SettlementCycle = "月付",
			ServiceScopeJson = SerializeList(["全天护理", "夜间巡检", "体征监控"]),
			AddOnsJson = SerializeList(["家属日报", "康复跟踪"]),
			BoundElders = 31,
			Status = "已生效",
			CreatedAtUtc = now.AddDays(-40),
			PublishedAtUtc = now.AddDays(-35),
			PricingNote = "夜班响应 SLA 已纳入价格模型。",
		},
		new ServicePackageEntity
		{
			PackageId = "PKG003",
			TenantId = tenantId,
			Name = "康复增强包",
			CareLevel = "专项康复",
			TargetGroup = "术后恢复与重点康复人群",
			MonthlyPrice = "¥1,800",
			SettlementCycle = "双周结",
			ServiceScopeJson = SerializeList(["康复计划", "训练记录", "家属反馈"]),
			AddOnsJson = SerializeList(["外部康复师支持"]),
			BoundElders = 12,
			Status = "待定价",
			CreatedAtUtc = now.AddDays(-15),
			PricingNote = "待财务确认外部康复师成本。",
		},
	};
	var plans = new[]
	{
		new ServicePlanEntity
		{
			PlanId = "PLAN001",
			TenantId = tenantId,
			PackageId = "PKG002",
			PackageName = "全护照护包",
			ElderlyName = "张秀英",
			Room = "101-1",
			CareLevel = "全护",
			Focus = "午间翻身护理 + 晚间血氧监测",
			ShiftSummary = "早班 / 晚班",
			OwnerRole = "护士",
			OwnerName = "李护士",
			RiskTagsJson = SerializeList(["血压偏高"]),
			Source = "套餐生成",
			Status = "执行中",
			CreatedAtUtc = now.AddDays(-7),
			ReviewNote = "主管已复核，纳入本周执行清单。",
		},
		new ServicePlanEntity
		{
			PlanId = "PLAN002",
			TenantId = tenantId,
			PackageId = "PKG003",
			PackageName = "康复增强包",
			ElderlyName = "李淑芳",
			Room = "301-1",
			CareLevel = "专项康复",
			Focus = "下肢训练 + 下午家属反馈",
			ShiftSummary = "中班",
			OwnerRole = "康复师",
			OwnerName = "黄康复师",
			RiskTagsJson = SerializeList(["术后恢复"]),
			Source = "套餐生成",
			Status = "待复核",
			CreatedAtUtc = now.AddDays(-6),
			ReviewNote = "待主管确认是否叠加外部康复师。",
		},
		new ServicePlanEntity
		{
			PlanId = "PLAN003",
			TenantId = tenantId,
			PackageId = "PKG001",
			PackageName = "半自理标准包",
			ElderlyName = "周玉兰",
			Room = "102-2",
			CareLevel = "半自理",
			Focus = "跌倒预警后加做晚间巡房",
			ShiftSummary = "晚班",
			OwnerRole = "护工",
			OwnerName = "张护工",
			RiskTagsJson = SerializeList(["跌倒高风险"]),
			Source = "临时插单",
			Status = "异常插单",
			CreatedAtUtc = now.AddDays(-5),
			ReviewNote = "来源于晚间告警，需连续观察 3 天。",
		},
	};

	await dbContext.ServicePackages.AddRangeAsync(packages, cancellationToken);
	await dbContext.ServicePlans.AddRangeAsync(plans, cancellationToken);
	await dbContext.SaveChangesAsync(cancellationToken);

	foreach (var plan in plans)
	{
		await SyncAssignmentsForPlanAsync(dbContext, plan, now, cancellationToken);
	}

	await dbContext.SaveChangesAsync(cancellationToken);
}

static async Task SyncAssignmentsForPlanAsync(CareDbContext dbContext, ServicePlanEntity plan, DateTimeOffset now, CancellationToken cancellationToken)
{
	var existing = await dbContext.ServicePlanAssignments.Where(item => item.PlanId == plan.PlanId).ToListAsync(cancellationToken);
	if (existing.Count > 0)
	{
		dbContext.ServicePlanAssignments.RemoveRange(existing);
	}
	if (plan.Status == "待复核" || plan.Status == "已归档" || string.IsNullOrWhiteSpace(plan.OwnerName) || plan.OwnerName == "待分派")
	{
		return;
	}

	var staffing = ResolveStaffingProfile(plan.OwnerName, plan.OwnerRole);
	foreach (var day in GetDaysOfWeek())
	{
		foreach (var shift in SplitShiftList(plan.ShiftSummary))
		{
			await dbContext.ServicePlanAssignments.AddAsync(new ServicePlanAssignmentEntity
			{
				AssignmentId = $"ASN-{plan.PlanId}-{day}-{shift}",
				TenantId = plan.TenantId,
				PlanId = plan.PlanId,
				ElderlyName = plan.ElderlyName,
				PackageName = plan.PackageName,
				Room = plan.Room,
				StaffName = plan.OwnerName,
				StaffRole = plan.OwnerRole,
				EmploymentSource = staffing.EmploymentSource,
				PartnerAgencyName = staffing.PartnerAgencyName,
				DayLabel = day,
				Shift = shift,
				Status = plan.Status,
				CreatedAtUtc = now,
			}, cancellationToken);
		}
	}
}

static ServicePlanTaskResponse[] BuildTaskResponses(IReadOnlyList<ServicePlanEntity> plans, IReadOnlyList<ServicePlanTaskExecutionEntity> taskExecutions)
{
	var executionMap = taskExecutions.ToDictionary(item => item.TaskExecutionId, item => item);
	return plans
		.Where(item => item.Status != "待复核")
		.Select(plan =>
		{
			var taskId = GetTaskId(plan.PlanId);
			executionMap.TryGetValue(taskId, out var execution);
			var riskTags = DeserializeList(plan.RiskTagsJson);
			return new ServicePlanTaskResponse(
				Id: taskId,
				PlanId: plan.PlanId,
				ElderlyName: plan.ElderlyName,
				Room: plan.Room,
				Title: plan.Focus,
				Owner: $"{plan.OwnerRole} / {plan.OwnerName}",
				OwnerName: plan.OwnerName,
				OwnerRole: plan.OwnerRole,
				Reminder: riskTags.Count > 0 ? $"风险标签 {string.Join('、', riskTags)}" : $"班次 {plan.ShiftSummary}",
				ScheduledTime: ResolveScheduledTime(plan.ShiftSummary),
				Shift: plan.ShiftSummary,
				CareLevel: plan.CareLevel,
				Priority: ResolveTaskPriority(plan.CareLevel),
				Status: execution?.Status ?? (plan.Status == "已归档" ? "已完成" : "待执行"),
				SourceId: plan.PlanId,
				SourceStatus: plan.Status == "已归档" ? "已入住" : "计划已生成",
				OriginStatusLabel: plan.Status,
				OriginLabel: "服务计划",
				PackageName: plan.PackageName,
				HandledBy: execution?.HandledBy,
				HandledAt: execution?.HandledAtUtc is null ? null : FormatShortStamp(execution.HandledAtUtc.Value),
				HandledAtIso: execution?.HandledAtUtc?.ToString("O"),
				ActionNote: execution?.ActionNote);
		})
		.OrderBy(item => item.ScheduledTime)
		.ToArray();
}

static CareScheduleBoardResponse BuildScheduleBoard(IReadOnlyList<ServicePlanEntity> plans, IReadOnlyList<ServicePlanAssignmentEntity> assignments)
{
	var planMap = plans.ToDictionary(item => item.PlanId, item => item);
	var rows = assignments
		.GroupBy(item => new { item.StaffName, item.StaffRole, item.EmploymentSource, item.PartnerAgencyName })
		.Select(group =>
		{
			var planIds = group.Select(item => item.PlanId).Distinct().ToArray();
			return new CareScheduleStaffRowResponse(
				StaffId: GetStaffId(group.Key.StaffName),
				StaffName: group.Key.StaffName,
				StaffRole: group.Key.StaffRole,
				EmploymentSource: group.Key.EmploymentSource,
				PartnerAgencyName: group.Key.PartnerAgencyName,
				AssignedPlans: planIds.Length,
				ExceptionPlans: planIds.Count(planId => planMap.GetValueOrDefault(planId)?.Status == "异常插单"),
				PendingReviewPlans: plans.Count(plan => plan.OwnerName == group.Key.StaffName && plan.Status == "待复核"),
				Cells: GetDaysOfWeek().Select(day => new CareScheduleCellResponse(
					DayLabel: day,
					Assignments: group.Where(item => item.DayLabel == day)
						.OrderBy(item => item.Shift)
						.Select(item => new CareScheduleAssignmentResponse(item.AssignmentId, item.PlanId, item.Shift, item.ElderlyName, item.PackageName, item.Room, item.Status))
						.ToArray()))
					.ToArray());
		})
		.OrderByDescending(item => item.AssignedPlans)
		.ThenBy(item => item.StaffName)
		.ToArray();

	var daySummaries = GetDaysOfWeek().Select(day => new CareScheduleDaySummaryResponse(
		DayLabel: day,
		Shifts: assignments.Where(item => item.DayLabel == day)
			.GroupBy(item => item.Shift)
			.Select(group => new CareScheduleShiftSummaryResponse(group.Key, group.Count()))
			.OrderBy(item => item.Shift)
			.ToArray()))
		.ToArray();

	var executablePlans = plans.Where(item => item.Status != "待复核" && item.Status != "已归档").ToArray();
	var attentionPlans = plans.Where(item => item.Status == "待复核" || ((string.IsNullOrWhiteSpace(item.OwnerName) || item.OwnerName == "待分派") && item.Status != "已归档"))
		.OrderByDescending(item => item.CreatedAtUtc)
		.Select(item => new CareScheduleAttentionPlanResponse(item.PlanId, item.ElderlyName, item.PackageName, item.OwnerRole, item.OwnerName, item.ShiftSummary, item.Status))
		.ToArray();

	return new CareScheduleBoardResponse(
		WeekLabel: "本周排班",
		ActivePlans: plans.Count(item => item.Status != "已归档"),
		PendingReviewPlans: plans.Count(item => item.Status == "待复核"),
		UnassignedPlans: executablePlans.Count(item => string.IsNullOrWhiteSpace(item.OwnerName) || item.OwnerName == "待分派"),
		ThirdPartyAssignedPlans: assignments.Where(item => item.EmploymentSource == "第三方合作").Select(item => item.PlanId).Distinct().Count(),
		PublishedAssignments: assignments.Count,
		ShiftDemand: assignments.GroupBy(item => item.Shift).Select(group => new CareScheduleShiftSummaryResponse(group.Key, group.Count())).OrderByDescending(item => item.Count).ToArray(),
		StaffRows: rows,
		DaySummaries: daySummaries,
		AttentionPlans: attentionPlans);
}

static async Task AppendWorkflowAuditAsync(CareDbContext dbContext, PlatformRequestContext requestContext, string aggregateType, string aggregateId, string actionType, object details, DateTimeOffset now, CancellationToken cancellationToken)
{
	await dbContext.CareWorkflowAudits.AddAsync(new CareWorkflowAuditEntity
	{
		AuditId = $"AUD-{aggregateType}-{aggregateId}-{now.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		AggregateType = aggregateType,
		AggregateId = aggregateId,
		ActionType = actionType,
		OperatorUserId = requestContext.UserId,
		OperatorUserName = requestContext.UserName,
		CorrelationId = requestContext.CorrelationId,
		DetailJson = JsonSerializer.Serialize(details),
		CreatedAtUtc = now,
	}, cancellationToken);
}

static ServicePackageActionResponse UpdatePackageState(ServicePackageEntity entity, string status, string pricingNote, string actionType)
{
	entity.Status = status;
	entity.PricingNote = pricingNote;
	return new ServicePackageActionResponse(entity.PackageId, entity.Status, actionType);
}

static ServicePackageActionResponse PublishPackage(ServicePackageEntity entity, DateTimeOffset now)
{
	entity.Status = "已生效";
	entity.PublishedAtUtc = now;
	entity.PricingNote = "套餐已发布，可进入老人绑定与计划生成。";
	return new ServicePackageActionResponse(entity.PackageId, entity.Status, "Publish");
}

static ServicePlanActionResponse ReviewPlan(ServicePlanEntity plan)
{
	plan.Status = "执行中";
	plan.ReviewNote = "主管已复核，计划进入执行态。";
	return new ServicePlanActionResponse(plan.PlanId, plan.Status, "Review");
}

static ServicePlanActionResponse MarkPlanException(ServicePlanEntity plan)
{
	plan.Status = "异常插单";
	plan.Source = "临时插单";
	plan.ReviewNote = "计划被调整为异常插单，请重点跟进。";
	return new ServicePlanActionResponse(plan.PlanId, plan.Status, "MarkException");
}

static ServicePlanActionResponse ArchivePlan(ServicePlanEntity plan)
{
	plan.Status = "已归档";
	plan.ReviewNote = "计划执行完成并归档。";
	return new ServicePlanActionResponse(plan.PlanId, plan.Status, "Archive");
}

static ServicePackageResponse ToServicePackageResponse(ServicePackageEntity item)
{
	return new ServicePackageResponse(item.PackageId, item.Name, item.CareLevel, item.TargetGroup, item.MonthlyPrice, item.SettlementCycle, DeserializeList(item.ServiceScopeJson), DeserializeList(item.AddOnsJson), item.BoundElders, item.Status, FormatFullStamp(item.CreatedAtUtc), item.PublishedAtUtc is null ? null : FormatFullStamp(item.PublishedAtUtc.Value), item.PricingNote);
}

static ServicePlanResponse ToServicePlanResponse(ServicePlanEntity item)
{
	return new ServicePlanResponse(item.PlanId, item.ElderlyName, item.Room, item.PackageId, item.PackageName, item.CareLevel, item.Focus, item.ShiftSummary, item.OwnerRole, item.OwnerName, DeserializeList(item.RiskTagsJson), item.Source, item.Status, FormatFullStamp(item.CreatedAtUtc), item.ReviewNote);
}

static CareWorkflowAuditResponse ToAuditResponse(CareWorkflowAuditEntity item)
{
	return new CareWorkflowAuditResponse(item.AuditId, item.AggregateType, item.AggregateId, item.ActionType, item.OperatorUserName, item.CorrelationId, item.DetailJson, FormatFullStamp(item.CreatedAtUtc));
}

static string SerializeList(IEnumerable<string> values)
{
	return JsonSerializer.Serialize(values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray());
}

static IReadOnlyList<string> DeserializeList(string? raw)
{
	if (string.IsNullOrWhiteSpace(raw))
	{
		return Array.Empty<string>();
	}

	try
	{
		return JsonSerializer.Deserialize<string[]>(raw) ?? Array.Empty<string>();
	}
	catch
	{
		return Array.Empty<string>();
	}
}

static string[] SplitShiftList(string raw)
{
	return raw.Split(['/', '、', ',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static string ResolveScheduledTime(string shift)
{
	if (shift.Contains("早班", StringComparison.Ordinal)) return "07:00";
	if (shift.Contains("白班", StringComparison.Ordinal)) return "08:00";
	if (shift.Contains("中班", StringComparison.Ordinal)) return "12:00";
	if (shift.Contains("晚班", StringComparison.Ordinal)) return "18:00";
	if (shift.Contains("夜班", StringComparison.Ordinal)) return "20:00";
	return "09:00";
}

static string ResolveTaskPriority(string careLevel)
{
	return careLevel switch
	{
		"全护" => "高",
		"专项康复" => "高",
		"半自理" => "中",
		_ => "常规",
	};
}

static string FormatFullStamp(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

static string FormatShortStamp(DateTimeOffset value) => value.ToLocalTime().ToString("MM-dd HH:mm");

static DateTimeOffset? ParseTimestamp(string? value)
{
	return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}

static DateTimeOffset? ParseDisplayTimestamp(string? value)
{
	if (string.IsNullOrWhiteSpace(value))
	{
		return null;
	}

	var year = DateTimeOffset.Now.Year;
	return DateTimeOffset.TryParse($"{year}-{value}", out var parsed) ? parsed : null;
}

static string GetTaskId(string planId) => $"plan-task-{planId}";

static string GetPlanIdFromTaskId(string taskId) => taskId.Replace("plan-task-", string.Empty, StringComparison.Ordinal);

static string GetStaffId(string staffName)
{
	var compact = new string(staffName.Where(char.IsLetterOrDigit).ToArray());
	return string.IsNullOrWhiteSpace(compact) ? $"staff-{Math.Abs(staffName.GetHashCode())}" : $"staff-{compact.ToLowerInvariant()}";
}

static (string EmploymentSource, string? PartnerAgencyName) ResolveStaffingProfile(string ownerName, string ownerRole)
{
	return ownerName switch
	{
		"张护工" => ("第三方合作", "安康陪护中心"),
		"黄康复师" => ("第三方合作", "康养复健联盟"),
		_ => (ownerRole.Contains("第三方", StringComparison.Ordinal) ? "第三方合作" : "自营", ownerRole.Contains("第三方", StringComparison.Ordinal) ? "待确认合作机构" : null),
	};
}

static List<CareTaskEntity> BuildTasks(string tenantId, string elderId, string careLevel)
{
	(string Title, string AssigneeRole, string DueAtLabel)[] taskTemplates = careLevel switch
	{
		"全护理" => [("晨间生命体征巡查", "护士", "08:00"), ("喂餐与服药确认", "护理员", "12:00"), ("夜间翻身与睡眠观察", "护理员", "21:00")],
		"半自理" => [("血压与血糖复测", "护士", "09:00"), ("康复训练协助", "护理员", "15:00")],
		_ => [("晨间状态回访", "护理员", "09:30"), ("晚间安全巡查", "护理员", "20:30")]
	};

	return taskTemplates.Select((item, index) => new CareTaskEntity
	{
		TaskId = $"TASK-{elderId}-{index + 1}",
		TenantId = tenantId,
		ElderId = elderId,
		Title = item.Title,
		AssigneeRole = item.AssigneeRole,
		DueAtLabel = item.DueAtLabel,
		Status = "Pending",
	}).ToList();
}

static CareTaskResponse ToTaskResponse(CareTaskEntity task)
{
	return new CareTaskResponse(task.TaskId, task.ElderId, task.Title, task.AssigneeRole, task.DueAtLabel, task.Status);
}

static string[] GetDaysOfWeek() => ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];
