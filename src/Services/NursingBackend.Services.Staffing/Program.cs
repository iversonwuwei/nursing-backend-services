using NursingBackend.BuildingBlocks.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "staffing-service",
	ServiceType: "domain-service",
	BoundedContext: "staffing-and-shifts",
	Consumers: ["admin-bff", "nani-bff", "care-service"],
	Capabilities: ["staff-profile", "onboarding", "shift-scheduling", "workforce-assignment"]));

app.Run();
