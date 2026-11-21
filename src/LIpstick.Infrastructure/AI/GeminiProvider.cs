using System.Text;
using System.Text.Json;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Infrastructure.AI;

public class GeminiProvider : IAIProvider
{
    private readonly ISecureStorageService _secureStorage;
    private readonly HttpClient _httpClient;

    public string ProviderName => "Google Gemini";
    public bool IsConfigured { get; private set; }

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
            throw new AIProviderException(ProviderName, "Gemini API key is not configured.", "NO_API_KEY");
        }

        string model = string.IsNullOrWhiteSpace(request.ModelName) ? "gemini-2.0-flash" : request.ModelName;
        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var prompt = BuildPrompt(request);
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

        try
        {
            using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new AIProviderException(ProviderName, $"API returned status {response.StatusCode}: {responseString}", response.StatusCode.ToString());
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
        catch (Exception ex) when (ex is not AIProviderException && ex is not OperationCanceledException)
        {
            throw new AIProviderException(ProviderName, "Network or protocol error calling Gemini API", "NETWORK_ERROR", ex);
        }
    }

    private static string BuildPrompt(RecommendationRequest request)
    {
        return $@"You are an elite LinkedIn profile polish consultant. Rewrite the following {request.SectionName} to be natural, human, and outcome-driven.
CRITICAL RULES:
1. Include at least 1-2 concrete, verifiable numbers/metrics.
2. Zero AI slop: do NOT use delve, testament, spearhead, seamless, passionate, in the realm of.
3. Use active voice and natural sentence variation.
4. Voice context: {request.VoiceProfile}

CURRENT TEXT:
{request.CurrentText}

RETURN ONLY THE POLISHED REWRITE WITH NO CHATTER, QUOTES, OR PREAMBLE.";
    }
}
