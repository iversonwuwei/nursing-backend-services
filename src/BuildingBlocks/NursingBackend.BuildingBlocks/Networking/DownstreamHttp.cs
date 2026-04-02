using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using NursingBackend.BuildingBlocks.Context;

namespace NursingBackend.BuildingBlocks.Networking;

public static class DownstreamHttp
{
    public static HttpRequestMessage CreateJsonRequest(HttpMethod method, string url, HttpContext context, object? payload = null)
    {
        var request = new HttpRequestMessage(method, url);
        var requestContext = context.GetPlatformRequestContext();

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

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

        return request;
    }

    public static async Task<T?> ReadJsonAsync<T>(this HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }
}