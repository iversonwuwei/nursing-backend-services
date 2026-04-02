using System.Text;
using System.Text.Json;

namespace NursingBackend.BuildingBlocks.Context;

public sealed record PlatformAccessToken(
    string TenantId,
    string UserId,
    string UserName,
    string[] Roles,
    string[] Scopes,
    DateTimeOffset ExpiresAtUtc);

public static class PlatformAccessTokenCodec
{
    public static string Encode(PlatformAccessToken token)
    {
        var json = JsonSerializer.Serialize(token);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static PlatformAccessToken? TryReadFromAuthorizationHeader(string authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var tokenValue = authorizationHeader[7..].Trim();
        return TryDecode(tokenValue, out var token) ? token : null;
    }

    public static bool TryDecode(string value, out PlatformAccessToken? token)
    {
        try
        {
            var normalized = value.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
            var bytes = Convert.FromBase64String(normalized);
            token = JsonSerializer.Deserialize<PlatformAccessToken>(bytes);
            return token is not null;
        }
        catch
        {
            token = null;
            return false;
        }
    }
}