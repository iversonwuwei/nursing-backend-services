using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Networking;

namespace NursingBackend.Services.Billing;

public static class BillingOutboxNotificationDispatcher
{
	public static IReadOnlyList<NotificationDispatchRequest> BuildRequests(OutboxMessageEntity message, string correlationId)
	{
		if (!string.Equals(message.EventType, "InvoiceIssued", StringComparison.Ordinal))
		{
			return Array.Empty<NotificationDispatchRequest>();
		}

		using var document = JsonDocument.Parse(message.PayloadJson);
		var root = document.RootElement;
		var elderId = root.GetProperty("ElderId").GetString() ?? string.Empty;
		var elderName = root.GetProperty("ElderName").GetString() ?? "老人";
		var amount = root.GetProperty("Amount").GetDecimal();
		var dueAtUtc = root.GetProperty("DueAtUtc").GetDateTimeOffset();

		return
		[
			new NotificationDispatchRequest(
				Audience: "family",
				AudienceKey: elderId,
				Category: "billing-invoice",
				Title: $"{elderName} 有一笔待处理账单",
				Body: $"账单金额 {amount:F2} 元，到期时间 {dueAtUtc:yyyy-MM-dd HH:mm}，请尽快确认。",
				SourceService: "billing-service",
				SourceEntityId: message.AggregateId,
				CorrelationId: correlationId)
		];
	}

	public static async Task<int> DispatchPendingAsync(
		BillingDbContext dbContext,
		HttpClient client,
		HttpContext context,
		IConfiguration configuration,
		CancellationToken cancellationToken,
		int? maxMessages = null)
	{
		var batchSize = maxMessages ?? configuration.GetValue<int?>("Outbox:BatchSize") ?? 20;
		var pending = await dbContext.OutboxMessages
			.Where(item => item.DispatchedAtUtc == null && item.EventType == "InvoiceIssued")
			.OrderBy(item => item.CreatedAtUtc)
			.Take(batchSize)
			.ToListAsync(cancellationToken);

		var correlationId = context.GetPlatformRequestContext()?.CorrelationId ?? Guid.NewGuid().ToString("N");
		var dispatchUrl = $"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications/dispatch";
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