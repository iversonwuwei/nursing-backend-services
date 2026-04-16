using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Networking;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "api-gateway",
	ServiceType: "gateway",
	BoundedContext: "edge",
	Consumers: ["admin-web", "family-app", "nani-app"],
	Capabilities: ["tenant-resolution", "auth-forwarding", "rate-limiting", "route-governance"]));

app.MapGet("/api/gateway/bootstrap", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	try
	{
		var client = httpClientFactory.CreateClient();
		var identity = await GetJsonAsync<IdentityContextResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Identity", "http://localhost:5265")}/api/identity/me",
			cancellationToken);
		var tenant = await GetJsonAsync<TenantDescriptorResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Tenant", "http://localhost:5186")}/api/tenants/{requestContext.TenantId}",
			cancellationToken);

		return Results.Ok(new
		{
			identity,
			tenant,
			context = requestContext,
			generatedAtUtc = DateTimeOffset.UtcNow,
		});
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "gateway bootstrap 失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
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
