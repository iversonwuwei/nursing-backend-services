using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace NursingBackend.BuildingBlocks.Context;

public sealed record PlatformRequestContext(
    string CorrelationId,
    string TenantId,
    string UserId,
    string UserName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Scopes,
    bool IsAuthenticated);

public static class PlatformHeaderNames
{
    public const string CorrelationId = "X-Correlation-Id";
    public const string TenantId = "X-Tenant-Id";
    public const string UserId = "X-User-Id";
    public const string UserName = "X-User-Name";
    public const string UserRole = "X-User-Role";
	public const string InternalServiceKey = "X-Internal-Service-Key";
	public const string ProviderWebhookKey = "X-Provider-Webhook-Key";
    public const string ProviderSignature = "X-Provider-Signature";
    public const string ProviderTimestamp = "X-Provider-Timestamp";
}

public static class PlatformRequestContextExtensions
{
    private const string ItemKey = "platform-request-context";

    public static IApplicationBuilder UsePlatformRequestContext(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers[PlatformHeaderNames.CorrelationId].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N");
            }

            var accessToken = PlatformAccessTokenCodec.TryReadFromAuthorizationHeader(context.Request.Headers.Authorization.ToString());
            var principal = context.User;
            var tenantId = principal.FindFirst("tenant_id")?.Value
                ?? accessToken?.TenantId
                ?? context.Request.Headers[PlatformHeaderNames.TenantId].FirstOrDefault()
                ?? string.Empty;
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value
                ?? accessToken?.UserId
                ?? context.Request.Headers[PlatformHeaderNames.UserId].FirstOrDefault()
                ?? "anonymous";
            var userName = principal.Identity?.Name
                ?? principal.FindFirst(ClaimTypes.Name)?.Value
                ?? accessToken?.UserName
                ?? context.Request.Headers[PlatformHeaderNames.UserName].FirstOrDefault()
                ?? "anonymous";
            var roles = principal.FindAll(ClaimTypes.Role).Select(item => item.Value).ToArray();
            if (roles.Length == 0)
            {
                roles = accessToken?.Roles
                    ?? context.Request.Headers[PlatformHeaderNames.UserRole].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            var scopes = principal.FindAll("scope").Select(item => item.Value).ToArray();
            if (scopes.Length == 0)
            {
                scopes = accessToken?.Scopes ?? Array.Empty<string>();
            }

            var requestContext = new PlatformRequestContext(
                CorrelationId: correlationId,
                TenantId: tenantId,
                UserId: userId,
                UserName: userName,
                Roles: roles,
                Scopes: scopes,
                IsAuthenticated: principal.Identity?.IsAuthenticated == true || accessToken is not null || !string.IsNullOrWhiteSpace(userId));

            context.Items[ItemKey] = requestContext;
            context.Response.Headers[PlatformHeaderNames.CorrelationId] = correlationId;
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                context.Response.Headers[PlatformHeaderNames.TenantId] = tenantId;
            }

            await next();
        });
    }

    public static PlatformRequestContext? GetPlatformRequestContext(this HttpContext context)
    {
        return context.Items.TryGetValue(ItemKey, out var value) ? value as PlatformRequestContext : null;
    }
}