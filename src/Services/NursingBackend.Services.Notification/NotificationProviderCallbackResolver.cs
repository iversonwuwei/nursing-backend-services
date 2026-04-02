using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Notification;

public enum NotificationProviderLookupMode
{
	None = 0,
	NotificationId = 1,
	CorrelationAndSource = 2,
}

public static class NotificationProviderCallbackResolver
{
	public static NotificationProviderLookupMode DetermineLookupMode(NotificationProviderCallbackRequest request)
	{
		if (!string.IsNullOrWhiteSpace(request.NotificationId))
		{
			return NotificationProviderLookupMode.NotificationId;
		}

		if (!string.IsNullOrWhiteSpace(request.CorrelationId)
			&& !string.IsNullOrWhiteSpace(request.SourceService)
			&& !string.IsNullOrWhiteSpace(request.SourceEntityId))
		{
			return NotificationProviderLookupMode.CorrelationAndSource;
		}

		return NotificationProviderLookupMode.None;
	}

	public static async Task<NotificationMessageEntity?> ResolveAsync(NotificationDbContext dbContext, string tenantId, NotificationProviderCallbackRequest request, CancellationToken cancellationToken)
	{
		return DetermineLookupMode(request) switch
		{
			NotificationProviderLookupMode.NotificationId => await dbContext.Notifications
				.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.NotificationId == request.NotificationId, cancellationToken),
			NotificationProviderLookupMode.CorrelationAndSource => await dbContext.Notifications
				.Where(item => item.TenantId == tenantId
					&& item.CorrelationId == request.CorrelationId
					&& item.SourceService == request.SourceService
					&& item.SourceEntityId == request.SourceEntityId)
				.OrderByDescending(item => item.CreatedAtUtc)
				.FirstOrDefaultAsync(cancellationToken),
			_ => null,
		};
	}

	public static string ComposeChannel(string provider, string channel)
	{
		var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "provider" : provider.Trim();
		var normalizedChannel = string.IsNullOrWhiteSpace(channel) ? "callback" : channel.Trim();
		return $"{normalizedProvider}:{normalizedChannel}";
	}
}