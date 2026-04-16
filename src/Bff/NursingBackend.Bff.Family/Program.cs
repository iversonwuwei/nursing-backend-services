using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Networking;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "family-bff",
	ServiceType: "bff",
	BoundedContext: "family-edge",
	Consumers: ["family-app"],
	Capabilities: ["relationship-scoped-read-models", "family-messages", "visit-summary", "bill-summary"]));

app.MapGet("/api/family/elders/{elderId}/today-summary", async (string elderId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	try
	{
		var client = httpClientFactory.CreateClient();
		var elder = await GetJsonAsync<ElderProfileSummaryResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/{elderId}",
			cancellationToken);
		var health = await GetJsonAsync<HealthArchiveSummaryResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Health", "http://localhost:5197")}/api/health/elders/{elderId}/summary",
			cancellationToken);
		var feed = await GetJsonAsync<NaniTaskFeedResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/elders/{elderId}/task-feed",
			cancellationToken);
		var notifications = await GetJsonAsync<IReadOnlyList<NotificationMessageResponse>>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications?audience=family&audienceKey={elderId}",
			cancellationToken) ?? [];

		return Results.Ok(new FamilyTodaySummaryResponse(
			Elder: elder,
			Health: health,
			TodayTasks: feed.Tasks,
			Notifications: notifications,
			Narrative: $"{elder.ElderName} 当前护理等级为 {elder.CareLevel}，房间 {elder.RoomNumber}。今日已生成 {feed.Tasks.Count} 项护理任务，当前健康关注点：{health.RiskSummary}。"));
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "家属摘要聚合失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

// ── AI Orchestration Proxy ─────────────────────────────────────────────────

app.MapPost("/api/family/ai/today-summary", async (AiFamilyTodaySummaryRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/today-summary", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 今日摘要生成失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/family/ai/health-explain", async (AiHealthExplainRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/health-explain", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 健康解读失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/family/ai/visit-assistant", async (AiVisitAssistantRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/visit-assistant", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 探访助手失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/family/ai/visit-risk", async (AiVisitRiskRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/visit-risk", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 探访风险评估失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/family/ai/chat", async (AiChatRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/family-chat", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 问答失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.Run();

static string ResolveServiceUrl(IConfiguration configuration, string serviceName, string fallback)
{
	return configuration[$"ServiceEndpoints:{serviceName}"] ?? fallback;
}

static async Task<T> GetJsonAsync<T>(HttpClient client, HttpContext context, string url, CancellationToken cancellationToken)
{
	using var request = DownstreamHttp.CreateJsonRequest(HttpMethod.Get, url, context);
	using var response = await client.SendAsync(request, cancellationToken);
	response.EnsureSuccessStatusCode();
	return (await response.ReadJsonAsync<T>(cancellationToken))!;
}

static async Task<T> PostJsonAsync<T>(HttpClient client, HttpContext context, string url, object payload, CancellationToken cancellationToken)
{
	using var request = DownstreamHttp.CreateJsonRequest(HttpMethod.Post, url, context, payload);
	using var response = await client.SendAsync(request, cancellationToken);
	response.EnsureSuccessStatusCode();
	return (await response.ReadJsonAsync<T>(cancellationToken))!;
}
