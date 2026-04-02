using Microsoft.Extensions.Options;
using NursingBackend.BuildingBlocks.Auth;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "identity-service",
	ServiceType: "domain-service",
	BoundedContext: "identity-and-access",
	Consumers: ["api-gateway", "admin-bff", "family-bff", "nani-bff"],
	Capabilities: ["login", "token-issuance", "role-resolution", "device-session"]));

app.MapPost("/api/identity/dev-login", (DevLoginRequest request, IOptions<PlatformJwtOptions> jwtOptions) =>
{
	if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.UserName))
	{
		return Results.Problem(title: "tenantId、userId 和 userName 为必填项。", statusCode: StatusCodes.Status400BadRequest);
	}

	var token = new PlatformAccessToken(
		TenantId: request.TenantId,
		UserId: request.UserId,
		UserName: request.UserName,
		Roles: request.Roles,
		Scopes: request.Scopes,
		ExpiresAtUtc: DateTimeOffset.UtcNow.AddHours(8));

	return Results.Ok(new DevLoginResponse(
		AccessToken: PlatformJwtExtensions.CreateAccessToken(token, jwtOptions.Value),
		TokenType: "Bearer",
		ExpiresAtUtc: token.ExpiresAtUtc,
		TenantId: token.TenantId,
		UserId: token.UserId,
		UserName: token.UserName,
		Roles: token.Roles,
		Scopes: token.Scopes));
}).AllowAnonymous();

app.MapGet("/api/identity/me", (HttpContext context) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Unauthorized();
	}

	return Results.Ok(new IdentityContextResponse(
		TenantId: requestContext.TenantId,
		UserId: requestContext.UserId,
		UserName: requestContext.UserName,
		Roles: requestContext.Roles,
		Scopes: requestContext.Scopes,
		CorrelationId: requestContext.CorrelationId));
}).RequireAuthorization();

app.Run();
