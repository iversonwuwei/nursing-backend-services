using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.AiOrchestration;

public sealed class AiModelRouter(
	ICompletionClient completionClient,
	IAiResultCache cache,
	IServiceScopeFactory scopeFactory,
	IOptions<AiModelsConfig> modelsConfig,
	ILogger<AiModelRouter> logger,
	IMeterFactory meterFactory)
{
	private readonly Meter _meter = meterFactory.Create("NursingBackend.AiOrchestration");

	private Counter<long>? _requestsTotal;
	private Counter<long>? _requestsCached;
	private Counter<long>? _requestsFailed;
	private Histogram<double>? _latency;

	private void EnsureMetrics()
	{
		_requestsTotal ??= _meter.CreateCounter<long>("ai.requests.total");
		_requestsCached ??= _meter.CreateCounter<long>("ai.requests.cached");
		_requestsFailed ??= _meter.CreateCounter<long>("ai.requests.failed");
		_latency ??= _meter.CreateHistogram<double>("ai.latency.ms");
	}

	public async Task<AiResult<T>> ExecuteAsync<T>(
		string capability,
		string endpoint,
		object input,
		string systemPrompt,
		string userPrompt,
		string tenantId,
		string userId,
		Func<string, T> resultParser,
		CancellationToken cancellationToken)
	{
		EnsureMetrics();
		var config = modelsConfig.Value;
		var sw = Stopwatch.StartNew();

		if (!config.Capabilities.TryGetValue(capability, out var capConfig)
			|| !config.TryResolveCapability(capability, out var resolvedCapability)
			|| resolvedCapability is null)
		{
			return Unavailable<T>(capability, "Capability not configured.");
		}

		var resolved = resolvedCapability;
		var inputHash = RedisAiResultCache.ComputeInputHash(input);

		// Cache check
		var cachedJson = await cache.GetAsync(tenantId, capability, inputHash, cancellationToken);
		if (cachedJson is not null)
		{
			sw.Stop();
			_requestsTotal?.Add(1, new KeyValuePair<string, object?>("capability", capability));
			_requestsCached?.Add(1, new KeyValuePair<string, object?>("capability", capability));

			var cachedResult = JsonSerializer.Deserialize<T>(cachedJson);
			var cacheAuditId = await WriteAuditLogAsync(tenantId, userId, capability, resolved.Provider, resolved.Model, endpoint, inputHash, 0, 0, true, (int)sw.ElapsedMilliseconds, true, null, cancellationToken);

			return new AiResult<T>(
				Available: true,
				Capability: capability,
				Provider: resolved.Provider,
				Model: resolved.Model,
				Result: cachedResult,
				Cached: true,
				LatencyMs: (int)sw.ElapsedMilliseconds,
				TraceId: Activity.Current?.TraceId.ToString() ?? string.Empty,
				AuditId: cacheAuditId);
		}

		// Try completion
		var provider = resolved.Provider;
		var model = resolved.Model;

		if (provider == "mock")
		{
			var parsed = AiMockResponses.Create<T>(endpoint, input);
			var resultJson = JsonSerializer.Serialize(parsed);
			await cache.SetAsync(tenantId, capability, inputHash, resultJson, cancellationToken);
			sw.Stop();
			_requestsTotal?.Add(1,
				new KeyValuePair<string, object?>("capability", capability),
				new KeyValuePair<string, object?>("provider", provider));
			_latency?.Record(sw.ElapsedMilliseconds,
				new KeyValuePair<string, object?>("capability", capability));

			var auditId = await WriteAuditLogAsync(tenantId, userId, capability, provider, model, endpoint, inputHash,
				0, 0, false, (int)sw.ElapsedMilliseconds, true, "development mock provider", cancellationToken);

			return new AiResult<T>(
				Available: true,
				Capability: capability,
				Provider: provider,
				Model: model,
				Result: parsed,
				Cached: false,
				LatencyMs: (int)sw.ElapsedMilliseconds,
				TraceId: Activity.Current?.TraceId.ToString() ?? string.Empty,
				AuditId: auditId);
		}

		try
		{
			var messages = new List<CompletionMessage>
			{
				new("system", systemPrompt),
				new("user", userPrompt)
			};

			var response = await completionClient.CompleteAsync(new CompletionRequest(
				Provider: provider,
				Model: model,
				Messages: messages,
				Temperature: capConfig.Temperature,
				MaxTokens: capConfig.MaxTokens), cancellationToken);

			sw.Stop();
			_requestsTotal?.Add(1,
				new KeyValuePair<string, object?>("capability", capability),
				new KeyValuePair<string, object?>("provider", provider));
			_latency?.Record(sw.ElapsedMilliseconds,
				new KeyValuePair<string, object?>("capability", capability));

			var parsed = resultParser(response.Content);
			var resultJson = JsonSerializer.Serialize(parsed);
			await cache.SetAsync(tenantId, capability, inputHash, resultJson, cancellationToken);

			var auditId = await WriteAuditLogAsync(tenantId, userId, capability, provider, model, endpoint, inputHash,
				response.InputTokens, response.OutputTokens, false, (int)sw.ElapsedMilliseconds, true, null, cancellationToken);

			return new AiResult<T>(
				Available: true,
				Capability: capability,
				Provider: provider,
				Model: model,
				Result: parsed,
				Cached: false,
				LatencyMs: (int)sw.ElapsedMilliseconds,
				TraceId: Activity.Current?.TraceId.ToString() ?? string.Empty,
				AuditId: auditId);
		}
		catch (Exception ex)
		{
			sw.Stop();
			_requestsFailed?.Add(1,
				new KeyValuePair<string, object?>("capability", capability),
				new KeyValuePair<string, object?>("error_type", ex.GetType().Name));

			logger.LogError(ex, "AI completion failed for capability {Capability} with provider {Provider}", capability, provider);

			// Try fallback to local provider
			if (config.Routing.EnableLocalFallback && provider != "local" && config.Providers.ContainsKey("local"))
			{
				logger.LogInformation("Falling back to local provider for capability {Capability}", capability);
				try
				{
					var localModel = config.Providers["local"].DefaultModel;
					var messages = new List<CompletionMessage>
					{
						new("system", systemPrompt),
						new("user", userPrompt)
					};

					var fallbackResponse = await completionClient.CompleteAsync(new CompletionRequest(
						Provider: "local",
						Model: localModel,
						Messages: messages,
						Temperature: capConfig.Temperature,
						MaxTokens: capConfig.MaxTokens), cancellationToken);

					sw.Stop();
					var parsed = resultParser(fallbackResponse.Content);
					var resultJson = JsonSerializer.Serialize(parsed);
					await cache.SetAsync(tenantId, capability, inputHash, resultJson, cancellationToken);

					var auditId = await WriteAuditLogAsync(tenantId, userId, capability, "local", localModel, endpoint, inputHash,
						fallbackResponse.InputTokens, fallbackResponse.OutputTokens, false, (int)sw.ElapsedMilliseconds, true, $"Fallback from {provider}", cancellationToken);

					return new AiResult<T>(
						Available: true,
						Capability: capability,
						Provider: "local",
						Model: localModel,
						Result: parsed,
						Cached: false,
						LatencyMs: (int)sw.ElapsedMilliseconds,
						TraceId: Activity.Current?.TraceId.ToString() ?? string.Empty,
						AuditId: auditId);
				}
				catch (Exception fallbackEx)
				{
					logger.LogError(fallbackEx, "Local fallback also failed for capability {Capability}", capability);
				}
			}

			await WriteAuditLogAsync(tenantId, userId, capability, provider, model, endpoint, inputHash, 0, 0, false, (int)sw.ElapsedMilliseconds, false, ex.Message, cancellationToken);
			return Unavailable<T>(capability, ex.Message);
		}
	}

	private async Task<string> WriteAuditLogAsync(
		string tenantId, string userId, string capability, string provider, string model,
		string endpoint, string inputHash, int inputTokens, int outputTokens,
		bool cached, int latencyMs, bool success, string? errorMessage,
		CancellationToken cancellationToken)
	{
		var auditId = Guid.NewGuid().ToString("N");
		try
		{
			using var scope = scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
			db.AuditLogs.Add(new AiAuditLogEntity
			{
				AuditId = auditId,
				TenantId = tenantId,
				UserId = userId,
				Capability = capability,
				Provider = provider,
				Model = model,
				Endpoint = endpoint,
				InputHash = inputHash,
				InputSizeBytes = inputTokens * 4, // approximate
				OutputSizeBytes = outputTokens * 4,
				Cached = cached,
				LatencyMs = latencyMs,
				Success = success,
				ErrorMessage = errorMessage?.Length > 1024 ? errorMessage[..1024] : errorMessage,
				CreatedAtUtc = DateTimeOffset.UtcNow
			});
			await db.SaveChangesAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to write AI audit log for {Capability}", capability);
		}

		return auditId;
	}

	private static AiResult<T> Unavailable<T>(string capability, string reason) => new(
		Available: false,
		Capability: capability,
		Provider: string.Empty,
		Model: string.Empty,
		Result: default,
		Cached: false,
		LatencyMs: 0,
		TraceId: Activity.Current?.TraceId.ToString() ?? string.Empty,
		AuditId: string.Empty);
}
