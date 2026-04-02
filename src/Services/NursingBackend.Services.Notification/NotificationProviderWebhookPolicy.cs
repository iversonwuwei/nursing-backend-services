using System.Security.Cryptography;
using System.Text;
using NursingBackend.BuildingBlocks.Contracts;

namespace NursingBackend.Services.Notification;

public static class NotificationProviderWebhookPolicy
{
	private const string HexLowerEncoding = "HexLower";
	private const string HexUpperEncoding = "HexUpper";
	private const string Base64Encoding = "Base64";
	private const string TimestampDotBodyPayloadMode = "TimestampDotBody";
	private const string TimestampBodyPayloadMode = "TimestampBody";
	private const string BodyPayloadMode = "Body";

	public static NotificationProviderSignatureProfileOptions ResolveProfile(NotificationProviderCallbackOptions options, string provider)
	{
		return options.Profiles.FirstOrDefault(item => string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase))
			?? options.DefaultProfile;
	}

	public static bool IsAuthorized(string? configuredKey, string? providedKey)
	{
		if (string.IsNullOrWhiteSpace(configuredKey) || string.IsNullOrWhiteSpace(providedKey))
		{
			return false;
		}

		var configured = Encoding.UTF8.GetBytes(configuredKey);
		var provided = Encoding.UTF8.GetBytes(providedKey);
		return configured.Length == provided.Length && CryptographicOperations.FixedTimeEquals(configured, provided);
	}

	public static bool IsSignatureValid(NotificationProviderSignatureProfileOptions profile, string? providedSignature, string? providedTimestamp, string rawBody, TimeProvider timeProvider)
	{
		if (!string.Equals(profile.SignatureMode, "HmacSha256", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(profile.SignatureSecret)
			|| string.IsNullOrWhiteSpace(providedSignature))
		{
			return false;
		}

		if (!TryBuildPayload(profile, providedTimestamp, rawBody, timeProvider, out var payload))
		{
			return false;
		}

		if (!TryDecodeProvidedSignature(profile, providedSignature, out var providedHash))
		{
			return false;
		}

		using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(profile.SignatureSecret));
		var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
		return expectedHash.Length == providedHash.Length && CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
	}

	public static string BuildDedupeKey(NotificationProviderCallbackRequest request)
	{
		var providerMessageId = string.IsNullOrWhiteSpace(request.ProviderMessageId) ? "none" : request.ProviderMessageId.Trim();
		var notificationId = string.IsNullOrWhiteSpace(request.NotificationId) ? "none" : request.NotificationId.Trim();
		var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? "none" : request.CorrelationId.Trim();
		var occurredAt = request.OccurredAtUtc?.ToUnixTimeMilliseconds().ToString() ?? "none";
		return $"{request.Provider}|{providerMessageId}|{notificationId}|{correlationId}|{request.Status}|{occurredAt}";
	}

	public static NotificationDeliveryResultRequest ToDeliveryResultRequest(NotificationProviderCallbackRequest request)
	{
		return ToDeliveryResultRequest(request, null);
	}

	public static NotificationDeliveryResultRequest ToDeliveryResultRequest(NotificationProviderCallbackRequest request, NotificationProviderSignatureProfileOptions? profile)
	{
		var status = string.IsNullOrWhiteSpace(request.Status) ? "Delivered" : request.Status;
		if (profile is not null && profile.StatusMappings.TryGetValue(status, out var mapped))
		{
			status = mapped;
		}

		return new NotificationDeliveryResultRequest(
			Status: status,
			Channel: string.IsNullOrWhiteSpace(profile?.ChannelOverride) ? (string.IsNullOrWhiteSpace(request.Channel) ? "provider" : request.Channel) : profile!.ChannelOverride!,
			FailureCode: request.FailureCode,
			FailureReason: request.FailureReason);
	}

	private static bool TryBuildPayload(NotificationProviderSignatureProfileOptions profile, string? providedTimestamp, string rawBody, TimeProvider timeProvider, out string payload)
	{
		payload = string.Empty;

		if (string.Equals(profile.SignaturePayloadMode, BodyPayloadMode, StringComparison.OrdinalIgnoreCase))
		{
			payload = rawBody;
			return true;
		}

		if (string.IsNullOrWhiteSpace(providedTimestamp)
			|| !long.TryParse(providedTimestamp, out var unixSeconds))
		{
			return false;
		}

		var callbackTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
		var age = timeProvider.GetUtcNow() - callbackTime;
		if (age.Duration() > TimeSpan.FromSeconds(Math.Max(0, profile.TimestampToleranceSeconds)))
		{
			return false;
		}

		if (string.Equals(profile.SignaturePayloadMode, TimestampDotBodyPayloadMode, StringComparison.OrdinalIgnoreCase)
			|| string.IsNullOrWhiteSpace(profile.SignaturePayloadMode))
		{
			payload = $"{providedTimestamp}.{rawBody}";
			return true;
		}

		if (string.Equals(profile.SignaturePayloadMode, TimestampBodyPayloadMode, StringComparison.OrdinalIgnoreCase))
		{
			payload = $"{providedTimestamp}{rawBody}";
			return true;
		}

		return false;
	}

	private static bool TryDecodeProvidedSignature(NotificationProviderSignatureProfileOptions profile, string providedSignature, out byte[] signature)
	{
		signature = [];
		var normalized = providedSignature.Trim();
		if (!string.IsNullOrWhiteSpace(profile.SignaturePrefix))
		{
			if (!normalized.StartsWith(profile.SignaturePrefix, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			normalized = normalized[profile.SignaturePrefix.Length..].Trim();
		}

		try
		{
			if (string.Equals(profile.SignatureEncoding, Base64Encoding, StringComparison.OrdinalIgnoreCase))
			{
				signature = Convert.FromBase64String(normalized);
				return true;
			}

			if (string.Equals(profile.SignatureEncoding, HexLowerEncoding, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(profile.SignatureEncoding, HexUpperEncoding, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(profile.SignatureEncoding, "Hex", StringComparison.OrdinalIgnoreCase)
				|| string.IsNullOrWhiteSpace(profile.SignatureEncoding))
			{
				signature = Convert.FromHexString(normalized);
				return true;
			}
		}
		catch (FormatException)
		{
			return false;
		}

		return false;
	}
}