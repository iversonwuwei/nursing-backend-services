namespace NursingBackend.Services.AiOrchestration;

public sealed class AiProviderConfig
{
	public string? ApiKey { get; set; }
	public string BaseUrl { get; set; } = string.Empty;
	public string DefaultModel { get; set; } = string.Empty;
	public int TimeoutSeconds { get; set; } = 30;
	public int MaxRetries { get; set; } = 2;
}

public sealed class AiCapabilityConfig
{
	public string Provider { get; set; } = string.Empty;
	public string Model { get; set; } = string.Empty;
	public double Temperature { get; set; } = 0.3;
	public int MaxTokens { get; set; } = 1024;
}

public sealed class AiRoutingConfig
{
	public string DefaultProvider { get; set; } = "openai";
	public bool EnableLocalFallback { get; set; } = true;
}

public sealed class AiModelsConfig
{
	public Dictionary<string, AiProviderConfig> Providers { get; set; } = new();
	public Dictionary<string, AiCapabilityConfig> Capabilities { get; set; } = new();
	public AiRoutingConfig Routing { get; set; } = new();
}

public sealed record AiCapabilityResolution(
	string Capability,
	string Provider,
	string Model,
	string? ConfiguredProvider,
	string? ConfiguredModel,
	bool UsesProviderDefaultModel,
	string ConfigurationSource);

public static class AiModelsConfigExtensions
{
	public static bool TryResolveCapability(this AiModelsConfig config, string capability, out AiCapabilityResolution? resolution)
	{
		if (!config.Capabilities.TryGetValue(capability, out var capabilityConfig))
		{
			resolution = null;
			return false;
		}

		var configuredProvider = string.IsNullOrWhiteSpace(capabilityConfig.Provider)
			? null
			: capabilityConfig.Provider.Trim();
		var provider = configuredProvider ?? config.Routing.DefaultProvider.Trim();

		var configuredModel = string.IsNullOrWhiteSpace(capabilityConfig.Model)
			? null
			: capabilityConfig.Model.Trim();

		var usesProviderDefaultModel = false;
		var effectiveModel = configuredModel ?? string.Empty;
		if (string.IsNullOrWhiteSpace(effectiveModel)
			&& config.Providers.TryGetValue(provider, out var providerConfig)
			&& !string.IsNullOrWhiteSpace(providerConfig.DefaultModel))
		{
			effectiveModel = providerConfig.DefaultModel.Trim();
			usesProviderDefaultModel = true;
		}

		var configurationSource = configuredProvider is not null && configuredModel is not null
			? "capability-override"
			: configuredProvider is not null
				? (usesProviderDefaultModel ? "capability-provider+provider-model" : "capability-provider")
				: configuredModel is not null
					? "capability-model"
					: usesProviderDefaultModel
						? "routing-default+provider-model"
						: "unconfigured";

		resolution = new AiCapabilityResolution(
			Capability: capability,
			Provider: provider,
			Model: effectiveModel,
			ConfiguredProvider: configuredProvider,
			ConfiguredModel: configuredModel,
			UsesProviderDefaultModel: usesProviderDefaultModel,
			ConfigurationSource: configurationSource);
		return true;
	}
}

public sealed class CacheTtlConfig
{
	public int AiInferenceMinutes { get; set; } = 15;
	public int DashboardSeconds { get; set; } = 120;
	public int BffReadModelSeconds { get; set; } = 180;
	public int SessionMinutes { get; set; } = 30;
	public int ConfigSnapshotMinutes { get; set; } = 10;
}
