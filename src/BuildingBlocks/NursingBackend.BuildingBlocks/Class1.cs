using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NursingBackend.BuildingBlocks.Auth;
using NursingBackend.BuildingBlocks.Context;

namespace NursingBackend.BuildingBlocks.Hosting;

public sealed record PlatformServiceDescriptor(
	string ServiceName,
	string ServiceType,
	string BoundedContext,
	string[] Consumers,
	string[] Capabilities,
	bool TenantAware = true);

public static class PlatformHostingExtensions
{
	public static WebApplicationBuilder AddPlatformDefaults(this WebApplicationBuilder builder)
	{
		builder.Services.AddProblemDetails();
		builder.Services.AddHealthChecks();
		builder.Services.AddHttpContextAccessor();
		builder.Services.AddHttpClient();
		builder.Services.AddPlatformJwtAuthentication(builder.Configuration);
		builder.Services.AddPlatformTelemetry(builder.Configuration, builder.Environment.ApplicationName);
		builder.Services.AddAuthorization();
		builder.Services.AddSingleton(TimeProvider.System);
		return builder;
	}

	public static WebApplication MapPlatformEndpoints(this WebApplication app, PlatformServiceDescriptor descriptor)
	{
		app.UseAuthentication();
		app.UseAuthorization();
		app.UsePlatformRequestContext();

		app.MapGet("/", () => Results.Ok(new
		{
			service = descriptor.ServiceName,
			type = descriptor.ServiceType,
			boundedContext = descriptor.BoundedContext,
			tenantAware = descriptor.TenantAware,
		}));

		app.MapGet("/health", () => Results.Ok(new
		{
			status = "ok",
			service = descriptor.ServiceName,
			utc = DateTimeOffset.UtcNow,
		}));

		app.MapGet("/context", (HttpContext context) =>
		{
			var requestContext = context.GetPlatformRequestContext();
			return requestContext is null ? Results.Unauthorized() : Results.Ok(requestContext);
		});

		app.MapGet("/descriptor", () => Results.Ok(descriptor));
		app.MapGet("/capabilities", () => Results.Ok(descriptor.Capabilities.Select(capability => new { capability })));

		return app;
	}
}
