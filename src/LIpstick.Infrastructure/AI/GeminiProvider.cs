using System.Text;
using System.Text.Json;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Core.Services;

namespace LIpstick.Infrastructure.AI;

public class GeminiProvider : IAIProvider
{
    private readonly ISecureStorageService _secureStorage;
    private readonly HttpClient _httpClient;

    public string ProviderName => "Google Gemini";
    public bool IsConfigured { get; private set; }
    public string? CustomBaseUrl { get; set; }

    public GeminiProvider(ISecureStorageService secureStorage, HttpClient? httpClient = null)
    {
        _secureStorage = secureStorage;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<IReadOnlyList<ModelInfo>> ListAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = await _secureStorage.GetSecretAsync("GEMINI_API_KEY");
        IsConfigured = !string.IsNullOrWhiteSpace(apiKey);

        if (!IsConfigured)
        {
            return Array.Empty<ModelInfo>();
        }

        return new List<ModelInfo>
        {
            new("gemini-2.5-flash", "Gemini 2.5 Flash (Fast)", ProviderName, 1000000, 350, true),
            new("gemini-2.0-flash", "Gemini 2.0 Flash (Default)", ProviderName, 1000000, 400, true),
            new("gemini-1.5-pro", "Gemini 1.5 Pro (Deep Reasoning)", ProviderName, 2000000, 850, true)
        };
    }

    public async Task<string> GenerateRecommendationAsync(RecommendationRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await _secureStorage.GetSecretAsync("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AIProviderException(ProviderName, "Gemini API key is not configured.", AIErrorCodes.NoApiKey);
        }

        string model = string.IsNullOrWhiteSpace(request.ModelName) ? "gemini-2.0-flash" : request.ModelName;
        string baseUrl = !string.IsNullOrWhiteSpace(CustomBaseUrl)
            ? CustomBaseUrl
            : (request.RubricCriteria?.TryGetValue("CustomBaseUrl", out var customUrl) == true ? customUrl.ToString()! : "https://generativelanguage.googleapis.com");

        string endpoint = $"{baseUrl.TrimEnd('/')}/v1beta/models/{model}:generateContent";

        var prompt = PromptContextAssembler.AssemblePrompt(request);
        var requestPayload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.4,
                maxOutputTokens = 800
            }
        };

        var json = JsonSerializer.Serialize(requestPayload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Add("x-goog-api-key", apiKey);
        httpRequest.Content = content;

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
                        if (errElement.TryGetProperty("status", out var statusElement) &&
                            string.Equals(statusElement.GetString(), "RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase))
                        {
                            errorCode = AIErrorCodes.QuotaExceeded;
                        }
                    }
                }
                catch { }

                errorMessage = errorMessage.Replace(apiKey, "[REDACTED]");
                throw new AIProviderException(ProviderName, errorMessage, errorCode);
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            var candidates = root.GetProperty("candidates");
            if (candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                var parts = candidate.GetProperty("content").GetProperty("parts");
                if (parts.GetArrayLength() > 0)
                {
                    return parts[0].GetProperty("text").GetString() ?? string.Empty;
                }
            }

            return string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new AIProviderException(ProviderName, "Gemini request timed out.", AIErrorCodes.Timeout, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AIProviderException(ProviderName, "Network or protocol error calling Gemini API.", AIErrorCodes.NetworkError, ex);
        }
        catch (Exception ex) when (ex is not AIProviderException)
        {
            throw new AIProviderException(ProviderName, "Network or protocol error calling Gemini API.", AIErrorCodes.NetworkError, ex);
        }
    }
}
