using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Context;

namespace NursingBackend.ArchitectureTests;

public class UnitTest1
{
    [Fact]
    public void Platform_service_descriptor_is_tenant_aware_by_default()
    {
        var descriptor = new PlatformServiceDescriptor(
            ServiceName: "care-service",
            ServiceType: "domain-service",
            BoundedContext: "care-orchestration",
            Consumers: ["admin-bff", "nani-bff"],
            Capabilities: ["care-plan", "task-assignment"]);

        Assert.True(descriptor.TenantAware);
        Assert.Equal("care-service", descriptor.ServiceName);
    }

    [Fact]
    public void Development_access_token_roundtrip_preserves_tenant_and_roles()
    {
        var original = new PlatformAccessToken(
            TenantId: "tenant-demo",
            UserId: "admin-001",
            UserName: "admin",
            Roles: ["platform-admin", "ops-admin"],
            Scopes: ["admin", "care:write"],
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddHours(1));

        var encoded = PlatformAccessTokenCodec.Encode(original);
        var decoded = PlatformAccessTokenCodec.TryDecode(encoded, out var token);

        Assert.True(decoded);
        Assert.NotNull(token);
        Assert.Equal(original.TenantId, token!.TenantId);
        Assert.Contains("platform-admin", token.Roles);
        Assert.Contains("care:write", token.Scopes);
    }
}
