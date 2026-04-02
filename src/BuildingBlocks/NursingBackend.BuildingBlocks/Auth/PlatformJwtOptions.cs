namespace NursingBackend.BuildingBlocks.Auth;

public sealed class PlatformJwtOptions
{
    public string Issuer { get; init; } = "nursing-platform";
    public string Audience { get; init; } = "nursing-platform-clients";
    public string SigningKey { get; init; } = "nursing-platform-development-signing-key-2026";
    public int ExpiresInMinutes { get; init; } = 480;
}