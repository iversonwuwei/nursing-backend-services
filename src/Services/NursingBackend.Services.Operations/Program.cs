using NursingBackend.BuildingBlocks.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "operations-service",
	ServiceType: "domain-service",
	BoundedContext: "facility-device-supply-alert",
	Consumers: ["admin-bff", "nani-bff", "notification-service", "ai-orchestration-service"],
	Capabilities: ["facility-management", "equipment-lifecycle", "supply-lifecycle", "alert-case-management"]));

app.Run();
