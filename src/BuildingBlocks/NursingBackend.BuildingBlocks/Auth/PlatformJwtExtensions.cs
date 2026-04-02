using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NursingBackend.BuildingBlocks.Context;

namespace NursingBackend.BuildingBlocks.Auth;

public static class PlatformJwtExtensions
{
    public static IServiceCollection AddPlatformJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("Jwt").Get<PlatformJwtOptions>() ?? new PlatformJwtOptions();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));

        services.AddSingleton(Options.Create(options));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                };
            });

        return services;
    }

    public static string CreateAccessToken(PlatformAccessToken token, PlatformJwtOptions options)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, token.UserId),
            new(ClaimTypes.NameIdentifier, token.UserId),
            new(ClaimTypes.Name, token.UserName),
            new("tenant_id", token.TenantId),
        };

        claims.AddRange(token.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(token.Scopes.Select(scope => new Claim("scope", scope)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: token.ExpiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}