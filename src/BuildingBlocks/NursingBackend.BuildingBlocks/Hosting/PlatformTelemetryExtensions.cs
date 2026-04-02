using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NursingBackend.BuildingBlocks.Hosting;

public static class PlatformTelemetryExtensions
{
	public static IServiceCollection AddPlatformTelemetry(this IServiceCollection services, IConfiguration configuration, string applicationName)
	{
		var serviceName = string.IsNullOrWhiteSpace(applicationName) ? "NursingBackend.Service" : applicationName;
		var serviceNamespace = configuration["OTEL_SERVICE_NAMESPACE"] ?? configuration["OpenTelemetry:ServiceNamespace"] ?? "nursing-platform";
		var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? configuration["OpenTelemetry:Endpoint"];
		var protocol = ResolveProtocol(configuration["OTEL_EXPORTER_OTLP_PROTOCOL"] ?? configuration["OpenTelemetry:Protocol"]);

		services.AddOpenTelemetry()
			.ConfigureResource(resource => resource.AddService(serviceName: serviceName, serviceNamespace: serviceNamespace))
			.WithTracing(tracing =>
			{
				tracing.AddSource(serviceName);
				tracing.AddAspNetCoreInstrumentation();
				tracing.AddHttpClientInstrumentation();
				if (!string.IsNullOrWhiteSpace(endpoint))
				{
					tracing.AddOtlpExporter(options =>
					{
						options.Endpoint = new Uri(endpoint);
						options.Protocol = protocol;
					});
				}
			})
			.WithMetrics(metrics =>
			{
				metrics.AddAspNetCoreInstrumentation();
				metrics.AddHttpClientInstrumentation();
				metrics.AddRuntimeInstrumentation();
				metrics.AddMeter(serviceName);
				if (!string.IsNullOrWhiteSpace(endpoint))
				{
					metrics.AddOtlpExporter(options =>
					{
						options.Endpoint = new Uri(endpoint);
						options.Protocol = protocol;
					});
				}
			});

		return services;
	}

	private static OtlpExportProtocol ResolveProtocol(string? protocol)
	{
		return string.Equals(protocol, "http/protobuf", StringComparison.OrdinalIgnoreCase)
			? OtlpExportProtocol.HttpProtobuf
			: OtlpExportProtocol.Grpc;
	}
}