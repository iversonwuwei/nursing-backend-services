using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.Services.Notification;
using System.Security.Cryptography;
using System.Text;

namespace NursingBackend.ArchitectureTests;

public class NotificationProviderWebhookPolicyTests
{
	[Fact]
	public void Shared_key_authorization_requires_exact_match()
	{
		Assert.True(NotificationProviderWebhookPolicy.IsAuthorized("provider-key", "provider-key"));
		Assert.False(NotificationProviderWebhookPolicy.IsAuthorized("provider-key", "other-key"));
		Assert.False(NotificationProviderWebhookPolicy.IsAuthorized("provider-key", null));
	}

	[Fact]
	public void Provider_callback_maps_to_delivery_request()
	{
		var delivery = NotificationProviderWebhookPolicy.ToDeliveryResultRequest(new NotificationProviderCallbackRequest(
			Provider: "twilio",
			Channel: "sms",
			Status: "Failed",
			NotificationId: "NTF-1",
			CorrelationId: null,
			SourceService: null,
			SourceEntityId: null,
			ProviderMessageId: "provider-msg-1",
			FailureCode: "timeout",
			FailureReason: "provider timeout",
			OccurredAtUtc: DateTimeOffset.UtcNow));

		Assert.Equal("Failed", delivery.Status);
		Assert.Equal("sms", delivery.Channel);
		Assert.Equal("timeout", delivery.FailureCode);
	}

	[Fact]
	public void Signature_validation_accepts_valid_hmac()
	{
		var profile = new NotificationProviderSignatureProfileOptions
		{
			Provider = "twilio",
			SignatureMode = "HmacSha256",
			SignatureEncoding = "HexLower",
			SignaturePayloadMode = "TimestampDotBody",
			SignatureSecret = "provider-secret",
			TimestampToleranceSeconds = 300,
		};
		const string timestamp = "1711968000";
		const string body = "{\"provider\":\"twilio\"}";

		using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(profile.SignatureSecret!));
		var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();
		var timeProvider = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1711968000).AddMinutes(1));

		Assert.True(NotificationProviderWebhookPolicy.IsSignatureValid(profile, signature, timestamp, body, timeProvider));
		Assert.False(NotificationProviderWebhookPolicy.IsSignatureValid(profile, "bad-signature", timestamp, body, timeProvider));
	}

	[Fact]
	public void Signature_validation_accepts_base64_body_mode_with_prefix()
	{
		var profile = new NotificationProviderSignatureProfileOptions
		{
			Provider = "mail-provider",
			SignatureMode = "HmacSha256",
			SignatureEncoding = "Base64",
			SignaturePayloadMode = "Body",
			SignaturePrefix = "sha256=",
			SignatureSecret = "provider-secret",
		};
		const string body = "{\"provider\":\"mail-provider\"}";

		using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(profile.SignatureSecret!));
		var signature = $"sha256={Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)))}";
		var timeProvider = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1711968000));

		Assert.True(NotificationProviderWebhookPolicy.IsSignatureValid(profile, signature, null, body, timeProvider));
	}

	[Fact]
	public void Signature_validation_rejects_expired_timestamp()
	{
		var profile = new NotificationProviderSignatureProfileOptions
		{
			Provider = "twilio",
			SignatureMode = "HmacSha256",
			SignatureEncoding = "HexLower",
			SignaturePayloadMode = "TimestampDotBody",
			SignatureSecret = "provider-secret",
			TimestampToleranceSeconds = 60,
		};
		const string timestamp = "1711968000";
		const string body = "{\"provider\":\"twilio\"}";
		using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(profile.SignatureSecret!));
		var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();
		var timeProvider = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1711968000).AddMinutes(5));

		Assert.False(NotificationProviderWebhookPolicy.IsSignatureValid(profile, signature, timestamp, body, timeProvider));
	}

	[Fact]
	public void Signature_validation_rejects_missing_required_prefix()
	{
		var profile = new NotificationProviderSignatureProfileOptions
		{
			Provider = "mail-provider",
			SignatureMode = "HmacSha256",
			SignatureEncoding = "Base64",
			SignaturePayloadMode = "Body",
			SignaturePrefix = "sha256=",
			SignatureSecret = "provider-secret",
		};
		const string body = "{\"provider\":\"mail-provider\"}";

		using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(profile.SignatureSecret!));
		var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
		var timeProvider = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1711968000));

		Assert.False(NotificationProviderWebhookPolicy.IsSignatureValid(profile, signature, null, body, timeProvider));
	}

	[Fact]
	public void Dedupe_key_prefers_provider_message_id_and_status()
	{
		var request = new NotificationProviderCallbackRequest(
			Provider: "twilio",
			Channel: "sms",
			Status: "Delivered",
			NotificationId: "NTF-1",
			CorrelationId: "corr-1",
			SourceService: "billing-service",
			SourceEntityId: "INV-1",
			ProviderMessageId: "msg-1",
			FailureCode: null,
			FailureReason: null,
			OccurredAtUtc: DateTimeOffset.FromUnixTimeSeconds(1711968000));

		var key = NotificationProviderWebhookPolicy.BuildDedupeKey(request);

		Assert.Equal("twilio|msg-1|NTF-1|corr-1|Delivered|1711968000000", key);
	}

	[Fact]
	public void Delivery_mapping_uses_profile_override()
	{
		var profile = new NotificationProviderSignatureProfileOptions
		{
			ChannelOverride = "sms",
			StatusMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["undelivered"] = "Failed",
			},
		};

		var delivery = NotificationProviderWebhookPolicy.ToDeliveryResultRequest(new NotificationProviderCallbackRequest(
			Provider: "twilio",
			Channel: "voice",
			Status: "undelivered",
			NotificationId: "NTF-1",
			CorrelationId: null,
			SourceService: null,
			SourceEntityId: null,
			ProviderMessageId: null,
			FailureCode: null,
			FailureReason: null,
			OccurredAtUtc: null), profile);

		Assert.Equal("Failed", delivery.Status);
		Assert.Equal("sms", delivery.Channel);
	}

	private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}
}