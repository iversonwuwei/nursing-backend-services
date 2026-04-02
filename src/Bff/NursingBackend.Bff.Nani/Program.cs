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
