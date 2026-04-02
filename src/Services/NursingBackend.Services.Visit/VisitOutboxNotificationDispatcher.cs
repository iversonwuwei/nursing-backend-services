using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Networking;

namespace NursingBackend.Services.Visit;

public static class VisitOutboxNotificationDispatcher
{
	public static IReadOnlyList<NotificationDispatchRequest> BuildRequests(OutboxMessageEntity message, string correlationId)
	{
		if (!string.Equals(message.EventType, "VisitRequested", StringComparison.Ordinal))
		{
			return Array.Empty<NotificationDispatchRequest>();
		}

		using var document = JsonDocument.Parse(message.PayloadJson);
		var root = document.RootElement;
		var visitId = root.GetProperty("VisitId").GetString() ?? string.Empty;
		var elderId = root.GetProperty("ElderId").GetString() ?? string.Empty;
		var visitorName = root.GetProperty("VisitorName").GetString() ?? "家属";
		var plannedAtUtc = root.GetProperty("PlannedAtUtc").GetDateTimeOffset();

		return
		[
			new NotificationDispatchRequest(
				Audience: "family",
				AudienceKey: elderId,
				Category: "visit-request",
				Title: $"{visitorName} 的探视申请已提交",
				Body: $"预计探视时间 {plannedAtUtc:yyyy-MM-dd HH:mm}，当前状态为 Requested。",
				SourceService: "visit-service",
				SourceEntityId: visitId,
				CorrelationId: correlationId)
		];
	}

	public static async Task<int> DispatchPendingAsync(
		VisitDbContext dbContext,
		HttpClient client,
		HttpContext context,
		IConfiguration configuration,
		CancellationToken cancellationToken,
		int? maxMessages = null)
	{
		var batchSize = maxMessages ?? configuration.GetValue<int?>("Outbox:BatchSize") ?? 20;
		var pending = await dbContext.OutboxMessages
			.Where(item => item.DispatchedAtUtc == null && item.EventType == "VisitRequested")
			.OrderBy(item => item.CreatedAtUtc)
			.Take(batchSize)
			.ToListAsync(cancellationToken);

		var correlationId = context.GetPlatformRequestContext()?.CorrelationId ?? Guid.NewGuid().ToString("N");
		var dispatchUrl = $"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5317")}/api/notifications/dispatch";
		var dispatchedCount = 0;

		foreach (var message in pending)
		{
			var requests = BuildRequests(message, correlationId);
			if (requests.Count == 0)
			{
				continue;
			}

			var succeeded = true;
			foreach (var notification in requests)
			{
				using var request = DownstreamHttp.CreateJsonRequest(HttpMethod.Post, dispatchUrl, context, notification);
				using var response = await client.SendAsync(request, cancellationToken);
				if (!response.IsSuccessStatusCode)
				{
					succeeded = false;
					break;
				}
			}

			if (!succeeded)
			{
				continue;
			}

			message.DispatchedAtUtc = DateTimeOffset.UtcNow;
			dispatchedCount += 1;
		}

		if (dispatchedCount > 0)
		{
			await dbContext.SaveChangesAsync(cancellationToken);
		}

		return dispatchedCount;
	}

	private static string ResolveServiceUrl(IConfiguration configuration, string serviceName, string fallback)
	{
		return configuration[$"ServiceEndpoints:{serviceName}"] ?? fallback;
	}
}