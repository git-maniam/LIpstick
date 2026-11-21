using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Infrastructure.AI;

public class OpenAIProvider : IAIProvider
{
    private readonly ISecureStorageService _secureStorage;
    private readonly HttpClient _httpClient;

    public string ProviderName => "OpenAI";
    public bool IsConfigured { get; private set; }

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
            throw new AIProviderException(ProviderName, "OpenAI API key is not configured.", "NO_API_KEY");
        }

        string model = string.IsNullOrWhiteSpace(request.ModelName) ? "gpt-4o" : request.ModelName;
        const string endpoint = "https://api.openai.com/v1/chat/completions";

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
                    content = $"Rewrite this {request.SectionName} with concrete metrics and strong human voice:\n\n{request.CurrentText}"
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
                throw new AIProviderException(ProviderName, $"API returned status {response.StatusCode}: {responseString}", response.StatusCode.ToString());
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
        catch (Exception ex) when (ex is not AIProviderException && ex is not OperationCanceledException)
        {
            throw new AIProviderException(ProviderName, "Network or protocol error calling OpenAI API", "NETWORK_ERROR", ex);
        }
    }
}
