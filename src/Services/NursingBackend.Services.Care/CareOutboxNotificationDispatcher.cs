using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Networking;

namespace NursingBackend.Services.Care;

public static class CareOutboxNotificationDispatcher
{
	public static IReadOnlyList<NotificationDispatchRequest> BuildRequests(OutboxMessageEntity message, string correlationId)
	{
		if (!string.Equals(message.EventType, "CarePlanGenerated", StringComparison.Ordinal))
		{
			return Array.Empty<NotificationDispatchRequest>();
		}

		using var document = JsonDocument.Parse(message.PayloadJson);
		var root = document.RootElement;
		var elderId = root.GetProperty("ElderId").GetString() ?? string.Empty;
		var elderName = root.GetProperty("ElderName").GetString() ?? "老人";
		var careLevel = root.GetProperty("CareLevel").GetString() ?? "未分级";
		var taskCount = root.GetProperty("TaskCount").GetInt32();

		return
		[
			new NotificationDispatchRequest(
				Audience: "nani",
				AudienceKey: elderId,
				Category: "care-plan",
				Title: $"{elderName} 新护理任务已生成",
				Body: $"护理等级 {careLevel}，系统已生成 {taskCount} 项护理任务，请在班次内处理。",
				SourceService: "care-service",
				SourceEntityId: message.AggregateId,
				CorrelationId: correlationId)
		];
	}

	public static async Task<int> DispatchPendingAsync(
		CareDbContext dbContext,
		HttpClient client,
		HttpContext context,
		IConfiguration configuration,
		CancellationToken cancellationToken,
		int? maxMessages = null)
	{
		var batchSize = maxMessages ?? configuration.GetValue<int?>("Outbox:BatchSize") ?? 20;
		var pending = await dbContext.OutboxMessages
			.Where(item => item.DispatchedAtUtc == null && item.EventType == "CarePlanGenerated")
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