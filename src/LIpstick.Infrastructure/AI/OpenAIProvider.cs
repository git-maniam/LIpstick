using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Core.Services;

namespace LIpstick.Infrastructure.AI;

public class OpenAIProvider : IAIProvider
{
    private readonly ISecureStorageService _secureStorage;
    private readonly HttpClient _httpClient;

    public string ProviderName => "OpenAI";
    public bool IsConfigured { get; private set; }
    public string? CustomBaseUrl { get; set; }

    public OpenAIProvider(ISecureStorageService secureStorage, HttpClient? httpClient = null)
    {
        _secureStorage = secureStorage;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<IReadOnlyList<ModelInfo>> ListAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = await _secureStorage.GetSecretAsync("OPENAI_API_KEY");
        IsConfigured = !string.IsNullOrWhiteSpace(apiKey);

        if (!IsConfigured)
        {
            return Array.Empty<ModelInfo>();
        }

        return new List<ModelInfo>
        {
            new("gpt-4o", "GPT-4o (Omni)", ProviderName, 128000, 450, true),
            new("gpt-4o-mini", "GPT-4o mini (Fast)", ProviderName, 128000, 280, true),
            new("o3-mini", "o3-mini (Reasoning)", ProviderName, 200000, 950, true)
        };
    }

    public async Task<string> GenerateRecommendationAsync(RecommendationRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await _secureStorage.GetSecretAsync("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AIProviderException(ProviderName, "OpenAI API key is not configured.", AIErrorCodes.NoApiKey);
        }

        string model = string.IsNullOrWhiteSpace(request.ModelName) ? "gpt-4o" : request.ModelName;
        string baseUrl = !string.IsNullOrWhiteSpace(CustomBaseUrl)
            ? CustomBaseUrl
            : (request.RubricCriteria?.TryGetValue("CustomBaseUrl", out var customUrl) == true ? customUrl.ToString()! : "https://api.openai.com/v1");

        string endpoint = $"{baseUrl.TrimEnd('/')}/chat/completions";

        var prompt = PromptContextAssembler.AssemblePrompt(request);
        var payload = new
        {
            model = model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are an elite LinkedIn profile editor. Output ONLY the polished humanized draft without conversational preamble or formatting tags. Never use AI buzzwords (delve, seamless, testament, spearhead, passionate)."
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            temperature = 0.5,
            max_tokens = 700
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorCode = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.TooManyRequests => AIErrorCodes.RateLimitExceeded,
                    System.Net.HttpStatusCode.Forbidden => AIErrorCodes.QuotaExceeded,
                    System.Net.HttpStatusCode.Unauthorized => AIErrorCodes.Unauthorized,
                    _ => response.StatusCode.ToString()
                };

                string errorMessage = $"API returned status {response.StatusCode}";
                try
                {
                    using var errDoc = JsonDocument.Parse(responseString);
                    if (errDoc.RootElement.TryGetProperty("error", out var errElement))
                    {
                        if (errElement.TryGetProperty("message", out var msgElement))
                        {
                            errorMessage = msgElement.GetString() ?? errorMessage;
                        }
                        if (errElement.TryGetProperty("code", out var codeElement))
                        {
                            var codeStr = codeElement.GetString();
                            if (string.Equals(codeStr, "insufficient_quota", StringComparison.OrdinalIgnoreCase))
                            {
                                errorCode = AIErrorCodes.QuotaExceeded;
                            }
                        }
                    }
                }
                catch { }

                errorMessage = errorMessage.Replace(apiKey, "[REDACTED]");
                throw new AIProviderException(ProviderName, errorMessage, errorCode);
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            var choices = root.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                var message = choice.GetProperty("message");
                return message.GetProperty("content").GetString() ?? string.Empty;
            }

            return string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new AIProviderException(ProviderName, "OpenAI request timed out.", AIErrorCodes.Timeout, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AIProviderException(ProviderName, "Network or protocol error calling OpenAI API.", AIErrorCodes.NetworkError, ex);
        }
        catch (Exception ex) when (ex is not AIProviderException)
        {
            throw new AIProviderException(ProviderName, "Network or protocol error calling OpenAI API.", AIErrorCodes.NetworkError, ex);
        }
    }
}
