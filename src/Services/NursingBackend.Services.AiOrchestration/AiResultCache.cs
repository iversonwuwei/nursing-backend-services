using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace NursingBackend.Services.AiOrchestration;

public interface IAiResultCache
{
	Task<string?> GetAsync(string tenantId, string capability, string inputHash, CancellationToken cancellationToken);
	Task SetAsync(string tenantId, string capability, string inputHash, string resultJson, CancellationToken cancellationToken);
	Task InvalidateAsync(string tenantId, string capability, CancellationToken cancellationToken);
}

public sealed class RedisAiResultCache(
	IConnectionMultiplexer redis,
	IOptions<CacheTtlConfig> ttlConfig,
	ILogger<RedisAiResultCache> logger)
	: IAiResultCache
{
	private IDatabase Db => redis.GetDatabase();

	public async Task<string?> GetAsync(string tenantId, string capability, string inputHash, CancellationToken cancellationToken)
	{
		try
		{
			var key = FormatKey(tenantId, capability, inputHash);
			var value = await Db.StringGetAsync(key);
			return value.HasValue ? value.ToString() : null;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Redis cache read failed for {Capability}, proceeding without cache.", capability);
			return null;
		}
	}

	public async Task SetAsync(string tenantId, string capability, string inputHash, string resultJson, CancellationToken cancellationToken)
	{
		try
		{
			var key = FormatKey(tenantId, capability, inputHash);
			var ttl = TimeSpan.FromMinutes(ttlConfig.Value.AiInferenceMinutes);
			await Db.StringSetAsync(key, resultJson, ttl);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Redis cache write failed for {Capability}, proceeding without cache.", capability);
		}
	}

	public async Task InvalidateAsync(string tenantId, string capability, CancellationToken cancellationToken)
	{
		try
		{
			var server = redis.GetServers().FirstOrDefault();
			if (server is null) return;

			var pattern = $"nursing:ai:{tenantId}:{capability}:*";
			await foreach (var key in server.KeysAsync(pattern: pattern))
			{
				await Db.KeyDeleteAsync(key);
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Redis cache invalidation failed for {Capability}.", capability);
		}
	}

	private static string FormatKey(string tenantId, string capability, string inputHash)
		=> $"nursing:ai:{tenantId}:{capability}:{inputHash}";

	public static string ComputeInputHash(object input)
	{
		var json = JsonSerializer.Serialize(input);
		var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
		return Convert.ToHexStringLower(hashBytes)[..32];
	}
}

public sealed class NoOpAiResultCache : IAiResultCache
{
	public Task<string?> GetAsync(string tenantId, string capability, string inputHash, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
	public Task SetAsync(string tenantId, string capability, string inputHash, string resultJson, CancellationToken cancellationToken) => Task.CompletedTask;
	public Task InvalidateAsync(string tenantId, string capability, CancellationToken cancellationToken) => Task.CompletedTask;
}
