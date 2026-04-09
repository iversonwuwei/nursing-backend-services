using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.Services.AiOrchestration;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

// ── Configuration ──────────────────────────────────────────────────────────
builder.Services.Configure<AiModelsConfig>(builder.Configuration.GetSection("AiModels"));
builder.Services.Configure<CacheTtlConfig>(builder.Configuration.GetSection("CacheTtl"));

// ── Database ───────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("Postgres")
	?? "Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing";
builder.Services.AddDbContext<AiDbContext>(options => options.UseNpgsql(connectionString));

// ── Redis Cache ────────────────────────────────────────────────────────────
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
try
{
	var redisConfig = ConfigurationOptions.Parse(redisConnectionString);
	redisConfig.AbortOnConnectFail = false;
	redisConfig.ConnectTimeout = builder.Configuration.GetValue("Redis:ConnectTimeout", 5000);
	redisConfig.SyncTimeout = builder.Configuration.GetValue("Redis:SyncTimeout", 3000);
	builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig));
	builder.Services.AddSingleton<IAiResultCache, RedisAiResultCache>();
}
catch
{
	builder.Services.AddSingleton<IAiResultCache, NoOpAiResultCache>();
}

// ── AI Services ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ICompletionClient, OpenAiCompatibleCompletionClient>();
builder.Services.AddSingleton<AiModelRouter>();

var app = builder.Build();

// ── Database Initialization ────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
	await db.Database.EnsureCreatedAsync();
}

app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "ai-orchestration-service",
	ServiceType: "domain-service",
	BoundedContext: "ai-orchestration",
	Consumers: ["admin-bff", "family-bff", "nani-bff"],
	Capabilities: ["ai-assessment", "summary-generation", "explanation-layer", "inference-audit", "ai-governance", "multi-model-routing"]));

var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

// ── Admin AI Endpoints ─────────────────────────────────────────────────────

app.MapPost("/api/ai/dashboard-insights", async (AiDashboardInsightsRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"运营数据: 总老人数={request.TotalElders}, 活跃护理计划={request.ActiveCarePlans}, 待处理报警={request.OpenAlerts}, 待办任务={request.PendingTasks}, 入住率={request.OccupancyPercent}%。{(request.AdditionalContext is not null ? $" 补充: {request.AdditionalContext}" : "")}";
	var result = await router.ExecuteAsync<AiDashboardInsightsResponse>("summarizer", "/api/ai/dashboard-insights", request,
		AiPromptTemplates.DashboardInsightsSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiDashboardInsightsResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/health-risk", async (AiHealthRiskRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"老人: {request.ElderName}，血压: {request.BloodPressure}，心率: {request.HeartRate}，体温: {request.Temperature}，血糖: {request.BloodSugar}，血氧: {request.Oxygen}。{(request.CurrentMedications is not null ? $"当前用药: {request.CurrentMedications}" : "")}{(request.MedicalHistory is not null ? $"病史: {request.MedicalHistory}" : "")}";
	var result = await router.ExecuteAsync<AiHealthRiskResponse>("analyzer", "/api/ai/health-risk", request,
		AiPromptTemplates.HealthRiskSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiHealthRiskResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/alert-suggestion", async (AiAlertSuggestionRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"报警类型: {request.AlertType}，描述: {request.AlertDescription}，严重程度: {request.Severity}。{(request.ElderContext is not null ? $"老人背景: {request.ElderContext}" : "")}{(request.RecentHistory is not null ? $"近期记录: {request.RecentHistory}" : "")}";
	var result = await router.ExecuteAsync<AiAlertSuggestionResponse>("analyzer", "/api/ai/alert-suggestion", request,
		AiPromptTemplates.AlertSuggestionSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiAlertSuggestionResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/task-priority", async (AiTaskPriorityRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var tasks = string.Join("\n", request.Tasks.Select(t => $"- [{t.TaskId}] {t.Title} | 老人: {t.ElderName} | 护理等级: {t.CareLevel} | 截止: {t.DueAt} | 状态: {t.Status}"));
	var prompt = $"请对以下护理任务按优先级排序:\n{tasks}";
	var result = await router.ExecuteAsync<AiTaskPriorityResponse>("classifier", "/api/ai/task-priority", request,
		AiPromptTemplates.TaskPrioritySystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiTaskPriorityResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/admission-assessment", async (AiAdmissionAssessmentRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var alerts = string.Join(", ", request.MedicalAlerts);
	var prompt = $"老人: {request.ElderName}，年龄: {request.Age}，性别: {request.Gender}，申请护理等级: {request.RequestedCareLevel}，医学注意事项: {alerts}。{(request.FamilyNotes is not null ? $"家属备注: {request.FamilyNotes}" : "")}";
	var result = await router.ExecuteAsync<AiAdmissionAssessmentResponse>("classifier", "/api/ai/admission-assessment", request,
		AiPromptTemplates.AdmissionAssessmentSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiAdmissionAssessmentResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/ops-report", async (AiOpsReportRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"报告类型: {request.ReportType}，时间段: {request.Period}。{(request.MetricsJson is not null ? $"数据: {request.MetricsJson}" : "")}";
	var result = await router.ExecuteAsync<AiOpsReportResponse>("summarizer", "/api/ai/ops-report", request,
		AiPromptTemplates.OpsReportSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiOpsReportResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/financial-insights", async (AiResourceInsightsRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"资源类型: 财务数据。{(request.DataSummary is not null ? $"数据: {request.DataSummary}" : "")}";
	var result = await router.ExecuteAsync<AiResourceInsightsResponse>("summarizer", "/api/ai/financial-insights", request,
		AiPromptTemplates.FinancialInsightsSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiResourceInsightsResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/device-insights", async (AiAlertSuggestionRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"设备报警类型: {request.AlertType}，描述: {request.AlertDescription}，严重程度: {request.Severity}。{(request.ElderContext is not null ? $"设备上下文: {request.ElderContext}" : "")}";
	var result = await router.ExecuteAsync<AiAlertSuggestionResponse>("analyzer", "/api/ai/device-insights", request,
		AiPromptTemplates.DeviceInsightsSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiAlertSuggestionResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/incident-analysis", async (AiAlertSuggestionRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"事故类型: {request.AlertType}，描述: {request.AlertDescription}，严重程度: {request.Severity}。{(request.RecentHistory is not null ? $"近期记录: {request.RecentHistory}" : "")}";
	var result = await router.ExecuteAsync<AiAlertSuggestionResponse>("analyzer", "/api/ai/incident-analysis", request,
		AiPromptTemplates.IncidentAnalysisSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiAlertSuggestionResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/resource-insights", async (AiResourceInsightsRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"资源类型: {request.ResourceType}。{(request.DataSummary is not null ? $"数据: {request.DataSummary}" : "")}";
	var result = await router.ExecuteAsync<AiResourceInsightsResponse>("summarizer", "/api/ai/resource-insights", request,
		AiPromptTemplates.ResourceInsightsSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiResourceInsightsResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/elder-detail-action", async (AiAlertSuggestionRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"老人情况: {request.AlertDescription}，关注级别: {request.Severity}。{(request.ElderContext is not null ? $"老人背景: {request.ElderContext}" : "")}";
	var result = await router.ExecuteAsync<AiAlertSuggestionResponse>("analyzer", "/api/ai/elder-detail-action", request,
		AiPromptTemplates.ElderDetailActionSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiAlertSuggestionResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/chat", async (AiChatRequest request, HttpContext context, AiModelRouter router, AiDbContext db, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
	var normalizedRequest = request with { ConversationId = conversationId };

	// Load conversation history
	var history = await db.ConversationMessages
		.Where(m => m.ConversationId == conversationId && m.TenantId == (rc!.TenantId ?? ""))
		.OrderBy(m => m.CreatedAtUtc)
		.Take(20)
		.Select(m => $"[{m.Role}]: {m.Content}")
		.ToListAsync(ct);

	var contextStr = history.Count > 0 ? $"\n\n对话历史:\n{string.Join("\n", history)}" : "";
	var prompt = $"{request.Message}{contextStr}";

	var result = await router.ExecuteAsync<AiChatResponse>("chat", "/api/ai/chat",
		normalizedRequest,
		AiPromptTemplates.AdminChatSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "",
		text =>
		{
			var parsed = TryParseJson<AiChatResponse>(text);
			return parsed ?? new AiChatResponse(text, conversationId);
		}, ct);

	// Save conversation messages
	if (result.Available && result.Result is not null)
	{
		db.ConversationMessages.Add(new() { MessageId = Guid.NewGuid().ToString("N"), TenantId = rc?.TenantId ?? "", ConversationId = conversationId, UserId = rc?.UserId ?? "", Role = "user", Content = request.Message, CreatedAtUtc = DateTimeOffset.UtcNow });
		db.ConversationMessages.Add(new() { MessageId = Guid.NewGuid().ToString("N"), TenantId = rc?.TenantId ?? "", ConversationId = conversationId, UserId = rc?.UserId ?? "", Role = "assistant", Content = result.Result.Reply, CreatedAtUtc = DateTimeOffset.UtcNow });
		await db.SaveChangesAsync(ct);
	}

	return Results.Ok(result);
}).RequireAuthorization();

// ── Nani AI Endpoints ──────────────────────────────────────────────────────

app.MapPost("/api/ai/shift-summary", async (AiShiftSummaryRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"班次: {request.Shift}，已完成任务: {request.CompletedTasks}，待办: {request.PendingTasks}，报警: {request.Alerts}。{(request.Notes is not null ? $"备注: {request.Notes}" : "")}";
	var result = await router.ExecuteAsync<AiShiftSummaryResponse>("summarizer", "/api/ai/shift-summary", request,
		AiPromptTemplates.ShiftSummarySystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiShiftSummaryResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/care-copilot", async (AiAlertSuggestionRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"任务类型: {request.AlertType}，描述: {request.AlertDescription}，紧急程度: {request.Severity}。{(request.ElderContext is not null ? $"老人背景: {request.ElderContext}" : "")}";
	var result = await router.ExecuteAsync<AiAlertSuggestionResponse>("analyzer", "/api/ai/care-copilot", request,
		AiPromptTemplates.CareCopilotSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiAlertSuggestionResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/handover-draft", async (AiHandoverDraftRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var completed = string.Join(", ", request.CompletedItems);
	var pending = string.Join(", ", request.PendingItems);
	var alerts = string.Join(", ", request.Alerts);
	var prompt = $"从 {request.FromShift} 交接到 {request.ToShift}。已完成: {completed}。待办: {pending}。报警: {alerts}。";
	var result = await router.ExecuteAsync<AiHandoverDraftResponse>("generator", "/api/ai/handover-draft", request,
		AiPromptTemplates.HandoverDraftSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiHandoverDraftResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/escalation-draft", async (AiEscalationDraftRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"报警类型: {request.AlertType}，老人: {request.ElderName}，描述: {request.Description}，当前状态: {request.CurrentStatus}。";
	var result = await router.ExecuteAsync<AiEscalationDraftResponse>("generator", "/api/ai/escalation-draft", request,
		AiPromptTemplates.EscalationDraftSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiEscalationDraftResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

// ── Family AI Endpoints ────────────────────────────────────────────────────

app.MapPost("/api/ai/today-summary", async (AiFamilyTodaySummaryRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var notes = string.Join("; ", request.RecentNotes);
	var prompt = $"老人: {request.ElderName}，护理等级: {request.CareLevel}，健康摘要: {request.HealthSummary}，今日已完成任务: {request.CompletedTasks}，待办: {request.PendingTasks}。近期备注: {notes}。";
	var result = await router.ExecuteAsync<AiFamilyTodaySummaryResponse>("summarizer", "/api/ai/today-summary", request,
		AiPromptTemplates.FamilyTodaySummarySystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiFamilyTodaySummaryResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/health-explain", async (AiHealthExplainRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"老人: {request.ElderName}，指标: {request.MetricName}，数值: {request.MetricValue}，正常范围: {request.NormalRange}。{(request.TrendDescription is not null ? $"趋势: {request.TrendDescription}" : "")}";
	var result = await router.ExecuteAsync<AiHealthExplainResponse>("analyzer", "/api/ai/health-explain", request,
		AiPromptTemplates.HealthExplainSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiHealthExplainResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/visit-assistant", async (AiVisitAssistantRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var slots = request.PreferredTimeSlots is not null ? string.Join(", ", request.PreferredTimeSlots) : "无偏好";
	var prompt = $"老人: {request.ElderName}，护理等级: {request.CareLevel}，偏好时段: {slots}。{(request.RecentHealthSummary is not null ? $"近期健康: {request.RecentHealthSummary}" : "")}";
	var result = await router.ExecuteAsync<AiVisitAssistantResponse>("generator", "/api/ai/visit-assistant", request,
		AiPromptTemplates.VisitAssistantSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiVisitAssistantResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/visit-risk", async (AiVisitRiskRequest request, HttpContext context, AiModelRouter router, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var prompt = $"老人: {request.ElderName}，计划探视时段: {request.TimeSlot}。{(request.CurrentHealthStatus is not null ? $"当前健康状态: {request.CurrentHealthStatus}" : "")}";
	var result = await router.ExecuteAsync<AiVisitRiskResponse>("classifier", "/api/ai/visit-risk", request,
		AiPromptTemplates.VisitRiskSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "", ParseJson<AiVisitRiskResponse>, ct);
	return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/ai/family-chat", async (AiChatRequest request, HttpContext context, AiModelRouter router, AiDbContext db, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var conversationId = request.ConversationId ?? Guid.NewGuid().ToString("N");
	var normalizedRequest = request with { ConversationId = conversationId };

	var history = await db.ConversationMessages
		.Where(m => m.ConversationId == conversationId && m.TenantId == (rc!.TenantId ?? ""))
		.OrderBy(m => m.CreatedAtUtc)
		.Take(20)
		.Select(m => $"[{m.Role}]: {m.Content}")
		.ToListAsync(ct);

	var contextStr = history.Count > 0 ? $"\n\n对话历史:\n{string.Join("\n", history)}" : "";
	var prompt = $"{request.Message}{contextStr}";

	var result = await router.ExecuteAsync<AiChatResponse>("chat", "/api/ai/family-chat",
		normalizedRequest,
		AiPromptTemplates.FamilyChatSystem, prompt, rc?.TenantId ?? "", rc?.UserId ?? "",
		text =>
		{
			var parsed = TryParseJson<AiChatResponse>(text);
			return parsed ?? new AiChatResponse(text, conversationId);
		}, ct);

	if (result.Available && result.Result is not null)
	{
		db.ConversationMessages.Add(new() { MessageId = Guid.NewGuid().ToString("N"), TenantId = rc?.TenantId ?? "", ConversationId = conversationId, UserId = rc?.UserId ?? "", Role = "user", Content = request.Message, CreatedAtUtc = DateTimeOffset.UtcNow });
		db.ConversationMessages.Add(new() { MessageId = Guid.NewGuid().ToString("N"), TenantId = rc?.TenantId ?? "", ConversationId = conversationId, UserId = rc?.UserId ?? "", Role = "assistant", Content = result.Result.Reply, CreatedAtUtc = DateTimeOffset.UtcNow });
		await db.SaveChangesAsync(ct);
	}

	return Results.Ok(result);
}).RequireAuthorization();

// ── Governance Endpoints ───────────────────────────────────────────────────

app.MapGet("/api/ai/rules", async (HttpContext context, AiDbContext db, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var rules = await db.Rules
		.Where(r => r.TenantId == (rc!.TenantId ?? ""))
		.OrderBy(r => r.Priority)
		.Select(r => new AiRuleResponse(r.RuleId, r.RuleCode, r.RuleName, r.Description, r.Capability, r.IsEnabled, r.Priority, r.UpdatedAtUtc))
		.ToListAsync(ct);
	return Results.Ok(rules);
}).RequireAuthorization();

app.MapPatch("/api/ai/rules/{ruleId}/toggle", async (string ruleId, AiRuleToggleRequest request, HttpContext context, AiDbContext db, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var rule = await db.Rules.FirstOrDefaultAsync(r => r.RuleId == ruleId && r.TenantId == (rc!.TenantId ?? ""), ct);
	if (rule is null) return Results.NotFound();
	rule.IsEnabled = request.IsEnabled;
	rule.UpdatedAtUtc = DateTimeOffset.UtcNow;
	await db.SaveChangesAsync(ct);
	return Results.Ok(new AiRuleResponse(rule.RuleId, rule.RuleCode, rule.RuleName, rule.Description, rule.Capability, rule.IsEnabled, rule.Priority, rule.UpdatedAtUtc));
}).RequireAuthorization();

app.MapGet("/api/ai/models/status", async (HttpContext context, ICompletionClient client, IOptions<AiModelsConfig> config, CancellationToken ct) =>
{
	var cfg = config.Value;
	var results = new List<AiModelStatusResponse>();
	var providerStatus = new Dictionary<string, (bool IsReachable, int LatencyMs, DateTimeOffset CheckedAtUtc)>(StringComparer.OrdinalIgnoreCase);

	foreach (var capabilityName in cfg.Capabilities.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
	{
		if (!cfg.TryResolveCapability(capabilityName, out var resolvedCapability) || resolvedCapability is null)
		{
			continue;
		}

		if (!providerStatus.TryGetValue(resolvedCapability.Provider, out var status))
		{
			var stopwatch = Stopwatch.StartNew();
			var reachable = await client.IsReachableAsync(resolvedCapability.Provider, ct);
			stopwatch.Stop();
			status = (reachable, (int)stopwatch.ElapsedMilliseconds, DateTimeOffset.UtcNow);
			providerStatus[resolvedCapability.Provider] = status;
		}

		results.Add(new AiModelStatusResponse(
			Provider: resolvedCapability.Provider,
			Model: resolvedCapability.Model,
			Capability: capabilityName,
			IsReachable: status.IsReachable,
			LatencyMs: status.LatencyMs,
			CheckedAtUtc: status.CheckedAtUtc,
			ConfiguredProvider: resolvedCapability.ConfiguredProvider,
			ConfiguredModel: resolvedCapability.ConfiguredModel,
			UsesProviderDefaultModel: resolvedCapability.UsesProviderDefaultModel,
			ConfigurationSource: resolvedCapability.ConfigurationSource));
	}

	return Results.Ok(results);
}).RequireAuthorization();

app.MapGet("/api/ai/audit-logs", async (HttpContext context, AiDbContext db, CancellationToken ct, string? capability, int page = 1, int pageSize = 20) =>
{
	var rc = context.GetPlatformRequestContext();
	var query = db.AuditLogs.Where(a => a.TenantId == (rc!.TenantId ?? ""));
	if (!string.IsNullOrWhiteSpace(capability))
		query = query.Where(a => a.Capability == capability);

	var total = await query.CountAsync(ct);
	var items = await query
		.OrderByDescending(a => a.CreatedAtUtc)
		.Skip((page - 1) * pageSize)
		.Take(pageSize)
		.Select(a => new AiAuditLogResponse(a.AuditId, a.TenantId, a.UserId, a.Capability, a.Provider, a.Model, a.Endpoint, a.Cached, a.LatencyMs, a.Success, a.ErrorMessage, a.CreatedAtUtc))
		.ToListAsync(ct);
	return Results.Ok(new AiAuditLogListResponse(items, total, page, pageSize));
}).RequireAuthorization();

app.MapGet("/api/ai/audit-logs/{auditId}", async (string auditId, HttpContext context, AiDbContext db, CancellationToken ct) =>
{
	var rc = context.GetPlatformRequestContext();
	var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.AuditId == auditId && a.TenantId == (rc!.TenantId ?? ""), ct);
	if (audit is null) return Results.NotFound();
	return Results.Ok(new AiAuditLogResponse(audit.AuditId, audit.TenantId, audit.UserId, audit.Capability, audit.Provider, audit.Model, audit.Endpoint, audit.Cached, audit.LatencyMs, audit.Success, audit.ErrorMessage, audit.CreatedAtUtc));
}).RequireAuthorization();

app.Run();

// ── JSON Parse Helpers ─────────────────────────────────────────────────────

static T ParseJson<T>(string text)
{
	// Extract JSON from potential markdown code block
	var json = text.Trim();
	if (json.StartsWith("```"))
	{
		var firstNewline = json.IndexOf('\n');
		if (firstNewline > 0) json = json[(firstNewline + 1)..];
		var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
		if (lastFence > 0) json = json[..lastFence];
		json = json.Trim();
	}

	return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
		?? throw new InvalidOperationException($"Failed to parse AI response as {typeof(T).Name}");
}

static T? TryParseJson<T>(string text) where T : class
{
	try { return ParseJson<T>(text); }
	catch { return null; }
}
