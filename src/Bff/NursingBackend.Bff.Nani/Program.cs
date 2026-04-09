using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Networking;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "nani-bff",
	ServiceType: "bff",
	BoundedContext: "nani-edge",
	Consumers: ["nani-app"],
	Capabilities: ["shift-task-feed", "alert-workbench", "care-command-model", "mobile-session-context"]));

app.MapGet("/api/nani/elders/{elderId}/task-feed", async (string elderId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	try
	{
		var client = httpClientFactory.CreateClient();
		var feed = await GetJsonAsync<NaniTaskFeedResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5311")}/api/care/elders/{elderId}/task-feed",
			cancellationToken);
		var notifications = await GetJsonAsync<IReadOnlyList<NotificationMessageResponse>>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5317")}/api/notifications?audience=nani&audienceKey={elderId}",
			cancellationToken) ?? [];

		return Results.Ok(new NaniTaskBoardResponse(
			ElderId: feed.ElderId,
			ElderName: feed.ElderName,
			CareLevel: feed.CareLevel,
			Tasks: feed.Tasks,
			Notifications: notifications));
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护工任务 feed 聚合失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

// ── AI Orchestration Proxy ─────────────────────────────────────────────────

app.MapPost("/api/nani/ai/shift-summary", async (AiShiftSummaryRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/shift-summary", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 交班摘要生成失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/nani/ai/care-copilot", async (AiAlertSuggestionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/care-copilot", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 护理助手失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/nani/ai/handover-draft", async (AiHandoverDraftRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/handover-draft", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 交接班草稿生成失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/nani/ai/escalation-draft", async (AiEscalationDraftRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/escalation-draft", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 升级草稿生成失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
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
