using NursingBackend.BuildingBlocks.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "ai-orchestration-service",
	ServiceType: "domain-service",
	BoundedContext: "ai-orchestration",
	Consumers: ["admin-bff", "family-bff", "nani-bff"],
	Capabilities: ["ai-assessment", "summary-generation", "explanation-layer", "inference-audit"]));

app.Run();
