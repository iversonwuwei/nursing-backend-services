using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace NursingBackend.Services.AiOrchestration;

public sealed record CompletionMessage(string Role, string Content);

public sealed record CompletionRequest(
	string Provider,
	string Model,
	IReadOnlyList<CompletionMessage> Messages,
	double Temperature,
	int MaxTokens);

public sealed record CompletionResponse(string Content, int InputTokens, int OutputTokens, string Provider, string Model);

public interface ICompletionClient
{
	Task<CompletionResponse> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken);
	Task<bool> IsReachableAsync(string provider, CancellationToken cancellationToken);
}

public sealed class OpenAiCompatibleCompletionClient(
	IHttpClientFactory httpClientFactory,
	IOptions<AiModelsConfig> modelsConfig,
	ILogger<OpenAiCompatibleCompletionClient> logger)
	: ICompletionClient
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false
	};

	public async Task<CompletionResponse> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken)
	{
		var config = modelsConfig.Value;
		if (!config.Providers.TryGetValue(request.Provider, out var providerConfig))
		{
			throw new InvalidOperationException($"AI provider '{request.Provider}' is not configured.");
		}

		var client = httpClientFactory.CreateClient();
		client.Timeout = TimeSpan.FromSeconds(providerConfig.TimeoutSeconds);

		var payload = new
		{
			model = request.Model,
			messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
			temperature = request.Temperature,
			max_tokens = request.MaxTokens
		};

		var url = $"{providerConfig.BaseUrl.TrimEnd('/')}/chat/completions";
		using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
		httpRequest.Content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions),
			Encoding.UTF8,
			"application/json");

		if (!string.IsNullOrWhiteSpace(providerConfig.ApiKey))
		{
			httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);
		}

		var lastException = default(Exception);
		for (var attempt = 0; attempt <= providerConfig.MaxRetries; attempt++)
		{
			try
			{
				using var response = await client.SendAsync(httpRequest.Clone(), cancellationToken);
				response.EnsureSuccessStatusCode();
				var json = await response.Content.ReadAsStringAsync(cancellationToken);
				var doc = JsonDocument.Parse(json);

				var choice = doc.RootElement.GetProperty("choices")[0];
				var content = choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

				var inputTokens = 0;
				var outputTokens = 0;
				if (doc.RootElement.TryGetProperty("usage", out var usage))
				{
					inputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
					outputTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
				}

				return new CompletionResponse(content, inputTokens, outputTokens, request.Provider, request.Model);
			}
			catch (Exception ex) when (attempt < providerConfig.MaxRetries)
			{
				lastException = ex;
				logger.LogWarning(ex, "AI completion attempt {Attempt} failed for provider {Provider}, retrying...", attempt + 1, request.Provider);
				await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)), cancellationToken);
			}
			catch (Exception ex)
			{
				lastException = ex;
			}
		}

		throw new InvalidOperationException($"AI completion failed after {providerConfig.MaxRetries + 1} attempts for provider {request.Provider}.", lastException);
	}

	public async Task<bool> IsReachableAsync(string provider, CancellationToken cancellationToken)
	{
		if (provider == "mock")
		{
			return true;
		}

		var config = modelsConfig.Value;
		if (!config.Providers.TryGetValue(provider, out var providerConfig))
		{
			return false;
		}

		try
		{
			var client = httpClientFactory.CreateClient();
			client.Timeout = TimeSpan.FromSeconds(5);
			var url = $"{providerConfig.BaseUrl.TrimEnd('/')}/models";
			using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
			if (!string.IsNullOrWhiteSpace(providerConfig.ApiKey))
			{
				httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);
			}

			using var response = await client.SendAsync(httpRequest, cancellationToken);
			return response.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}
}

internal static class HttpRequestMessageExtensions
{
	public static HttpRequestMessage Clone(this HttpRequestMessage request)
	{
		var clone = new HttpRequestMessage(request.Method, request.RequestUri);
		if (request.Content is StringContent stringContent)
		{
			var contentString = stringContent.ReadAsStringAsync().GetAwaiter().GetResult();
			clone.Content = new StringContent(contentString, Encoding.UTF8, "application/json");
		}

		foreach (var header in request.Headers)
		{
			clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}

		return clone;
	}
}
