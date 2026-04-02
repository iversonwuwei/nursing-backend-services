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
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5310")}/api/elders/{elderId}",
			cancellationToken);
		var health = await GetJsonAsync<HealthArchiveSummaryResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Health", "http://localhost:5312")}/api/health/elders/{elderId}/summary",
			cancellationToken);
		var feed = await GetJsonAsync<NaniTaskFeedResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5311")}/api/care/elders/{elderId}/task-feed",
			cancellationToken);
		var notifications = await GetJsonAsync<IReadOnlyList<NotificationMessageResponse>>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5317")}/api/notifications?audience=family&audienceKey={elderId}",
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
