using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var tenants = new Dictionary<string, TenantDescriptorResponse>(StringComparer.OrdinalIgnoreCase)
{
	["tenant-demo"] = new(
		TenantId: "tenant-demo",
		TenantName: "演示养老集团",
		Plan: "saas-professional",
		DataIsolationMode: "shared-db-rls",
		EnabledModules: ["admin", "family", "nani", "billing", "ai"],
		EnabledFeatures: ["tenant-context", "visit-approval", "care-workflow", "family-summary"],
		Branches: ["浦东店", "静安店"]),
	["tenant-private"] = new(
		TenantId: "tenant-private",
		TenantName: "私有化试点机构",
		Plan: "enterprise-dedicated",
		DataIsolationMode: "database-per-tenant",
		EnabledModules: ["admin", "nani", "billing"],
		EnabledFeatures: ["tenant-context", "dedicated-db", "advanced-audit"],
		Branches: ["总部院区"]),
};

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "tenant-service",
	ServiceType: "domain-service",
	BoundedContext: "saas-tenancy",
	Consumers: ["api-gateway", "admin-bff", "all-domain-services"],
	Capabilities: ["tenant-provisioning", "feature-flags", "entitlements", "organization-topology"]));

app.MapGet("/api/tenants", () => Results.Ok(tenants.Values.OrderBy(item => item.TenantId))).RequireAuthorization();

app.MapGet("/api/tenants/{tenantId}", (string tenantId) =>
{
	return tenants.TryGetValue(tenantId, out var tenant)
		? Results.Ok(tenant)
		: Results.Problem(title: $"租户 {tenantId} 不存在。", statusCode: StatusCodes.Status404NotFound);
}).RequireAuthorization();

app.Run();
