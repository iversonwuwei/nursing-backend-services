namespace NursingBackend.Services.Notification;

public sealed class NotificationProviderCallbackOptions
{
	public string? SharedKey { get; set; }
	public bool AllowSharedKeyFallback { get; set; } = true;
	public string SharedKeyHeaderName { get; set; } = "X-Provider-Webhook-Key";
	public NotificationProviderSignatureProfileOptions DefaultProfile { get; set; } = new();
	public List<NotificationProviderSignatureProfileOptions> Profiles { get; set; } = [];
}

public sealed class NotificationProviderSignatureProfileOptions
{
	public string Provider { get; set; } = "default";
	public string SignatureMode { get; set; } = "HmacSha256";
	public string SignatureEncoding { get; set; } = "HexLower";
	public string SignaturePayloadMode { get; set; } = "TimestampDotBody";
	public string? SignaturePrefix { get; set; }
	public string SignatureHeaderName { get; set; } = "X-Provider-Signature";
	public string TimestampHeaderName { get; set; } = "X-Provider-Timestamp";
	public int TimestampToleranceSeconds { get; set; } = 300;
	public string? SignatureSecret { get; set; }
	public string? ChannelOverride { get; set; }
	public Dictionary<string, string> StatusMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}