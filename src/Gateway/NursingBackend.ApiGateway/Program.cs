using Microsoft.Extensions.Primitives;
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

app.MapMethods("/api/admin/{**path}", ["GET", "POST", "PUT", "PATCH", "DELETE"], async (string? path, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	var client = httpClientFactory.CreateClient();
	var downstreamUrl = BuildAdminProxyUrl(configuration, path, context.Request.QueryString.Value);

	try
	{
		using var request = await CreateProxyRequestAsync(context, downstreamUrl, cancellationToken);
		using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

		context.Response.StatusCode = (int)response.StatusCode;
		CopyProxyResponseHeaders(response, context.Response);
		await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
	}
	catch (Exception exception)
	{
		context.Response.StatusCode = StatusCodes.Status502BadGateway;
		context.Response.ContentType = "application/problem+json";
		await Results.Problem(
			title: "gateway admin proxy 失败。",
			detail: exception.Message,
			statusCode: StatusCodes.Status502BadGateway)
			.ExecuteAsync(context);
	}
}).RequireAuthorization();

app.Run();

static string ResolveServiceUrl(IConfiguration configuration, string serviceName, string fallback)
{
	return configuration[$"ServiceEndpoints:{serviceName}"] ?? fallback;
}

static string BuildAdminProxyUrl(IConfiguration configuration, string? path, string? queryString)
{
	var serviceUrl = ResolveServiceUrl(configuration, "AdminBff", "http://localhost:5146").TrimEnd('/');
	var adminPath = string.IsNullOrWhiteSpace(path)
		? "/api/admin"
		: $"/api/admin/{path.TrimStart('/')}";

	return string.IsNullOrWhiteSpace(queryString)
		? $"{serviceUrl}{adminPath}"
		: $"{serviceUrl}{adminPath}{queryString}";
}

static async Task<HttpRequestMessage> CreateProxyRequestAsync(HttpContext context, string url, CancellationToken cancellationToken)
{
	var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), url);
	var requestContext = context.GetPlatformRequestContext();

	if (context.Request.Headers.TryGetValue("Authorization", out var authorization))
	{
		request.Headers.TryAddWithoutValidation("Authorization", authorization.ToString());
	}

	if (!string.IsNullOrWhiteSpace(requestContext?.CorrelationId))
	{
		request.Headers.TryAddWithoutValidation(PlatformHeaderNames.CorrelationId, requestContext.CorrelationId);
	}
	else if (context.Request.Headers.TryGetValue(PlatformHeaderNames.CorrelationId, out var correlationId))
	{
		request.Headers.TryAddWithoutValidation(PlatformHeaderNames.CorrelationId, correlationId.ToString());
	}

	if (!string.IsNullOrWhiteSpace(requestContext?.TenantId))
	{
		request.Headers.TryAddWithoutValidation(PlatformHeaderNames.TenantId, requestContext.TenantId);
	}
	else if (context.Request.Headers.TryGetValue(PlatformHeaderNames.TenantId, out var tenantId))
	{
		request.Headers.TryAddWithoutValidation(PlatformHeaderNames.TenantId, tenantId.ToString());
	}

	if (RequestHasBody(context.Request))
	{
		var bodyStream = new MemoryStream();
		await context.Request.Body.CopyToAsync(bodyStream, cancellationToken);
		bodyStream.Position = 0;
		request.Content = new StreamContent(bodyStream);

		if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
		{
			request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
		}
		if (context.Request.Headers.TryGetValue("Content-Language", out var contentLanguage))
		{
			request.Content.Headers.TryAddWithoutValidation("Content-Language", contentLanguage.ToString());
		}
	}

	return request;
}

static void CopyProxyResponseHeaders(HttpResponseMessage source, HttpResponse target)
{
	foreach (var header in source.Headers)
	{
		target.Headers[header.Key] = new StringValues([.. header.Value]);
	}

	foreach (var header in source.Content.Headers)
	{
		target.Headers[header.Key] = new StringValues([.. header.Value]);
	}

	target.Headers.Remove("transfer-encoding");
	if (source.Content.Headers.ContentType is not null)
	{
		target.ContentType = source.Content.Headers.ContentType.ToString();
	}
}

static bool RequestHasBody(HttpRequest request)
{
	return request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding");
}

static async Task<T> GetJsonAsync<T>(HttpClient client, HttpContext context, string url, CancellationToken cancellationToken)
{
	using var request = DownstreamHttp.CreateJsonRequest(HttpMethod.Get, url, context);
	using var response = await client.SendAsync(request, cancellationToken);
	response.EnsureSuccessStatusCode();
	return (await response.ReadJsonAsync<T>(cancellationToken))!;
}
