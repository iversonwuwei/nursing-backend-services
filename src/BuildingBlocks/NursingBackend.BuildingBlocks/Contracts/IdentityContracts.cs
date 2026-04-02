namespace NursingBackend.BuildingBlocks.Contracts;

public sealed record DevLoginRequest(
    string TenantId,
    string UserId,
    string UserName,
    string[] Roles,
    string[] Scopes);

public sealed record DevLoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    string TenantId,
    string UserId,
    string UserName,
    string[] Roles,
    string[] Scopes);

public sealed record IdentityContextResponse(
    string TenantId,
    string UserId,
    string UserName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Scopes,
    string CorrelationId);