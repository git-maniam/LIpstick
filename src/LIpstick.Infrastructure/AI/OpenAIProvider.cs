using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
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

    public async Task<string> GenerateRecommendationAsync(RecommendationRequest request, CancellationToken cancellationToken = default) =>
        (await GenerateCompletionAsync(request, cancellationToken)).Text;

    public async Task<AICompletionResult> GenerateCompletionAsync(RecommendationRequest request, CancellationToken cancellationToken = default)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var apiKey = await _secureStorage.GetSecretAsync("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AIProviderException(ProviderName, "OpenAI API key is not configured.", AIErrorCodes.NoApiKey);
        }

        string model = string.IsNullOrWhiteSpace(request.ModelName) ? "gpt-4o" : request.ModelName;
        string baseUrl = !string.IsNullOrWhiteSpace(CustomBaseUrl)
            ? CustomBaseUrl
            : (request.RubricCriteria?.TryGetValue("CustomBaseUrl", out var customUrl) == true ? customUrl.ToString()! : "https://api.openai.com/v1");

        var prompt = PromptContextAssembler.AssemblePrompt(request);

        try
        {
            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri(baseUrl.TrimEnd('/') + "/"),
                Transport = new HttpClientPipelineTransport(_httpClient)
            };
            var client = new ChatClient(model, new ApiKeyCredential(apiKey), options);
            ChatMessage[] messages =
            {
                new SystemChatMessage("You are an elite LinkedIn profile editor. Output ONLY the polished humanized draft without conversational preamble or formatting tags. Never use AI buzzwords (delve, seamless, testament, spearhead, passionate)."),
                new UserChatMessage(prompt)
            };
            var result = await client.CompleteChatAsync(messages, new ChatCompletionOptions { MaxOutputTokenCount = 700 }, cancellationToken);
            var completion = result.Value;
            string responseJson = result.GetRawResponse().Content.ToString();
            timer.Stop();
            string text = string.Join("", completion.Content.Select(part => part.Text));
            return ExtractUsage(responseJson, text, model, (int)timer.ElapsedMilliseconds);
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
        catch (ClientResultException ex)
        {
            string code = ex.Status switch
            {
                401 => AIErrorCodes.InvalidKey,
                403 => AIErrorCodes.QuotaExceeded,
                429 => AIErrorCodes.RateLimitExceeded,
                >= 200 and < 300 => AIErrorCodes.MalformedResponse,
                _ => AIErrorCodes.NetworkError
            };
            throw new AIProviderException(ProviderName, "OpenAI request failed. Check credentials, quota, and endpoint settings.", code, ex);
        }
        catch (Exception ex) when (ex is not AIProviderException)
        {
            throw new AIProviderException(ProviderName, "Network or protocol error calling OpenAI API.", AIErrorCodes.NetworkError, ex);
        }
    }

    private AICompletionResult ExtractUsage(string responseJson, string text, string model, int latencyMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new AIProviderException(ProviderName, "OpenAI returned no generated text.", AIErrorCodes.MalformedResponse);

        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            int? promptTokens = null;
            int? responseTokens = null;
            if (doc.RootElement.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var prompt) && prompt.TryGetInt32(out var promptCount)) promptTokens = promptCount;
                if (usage.TryGetProperty("completion_tokens", out var response) && response.TryGetInt32(out var responseCount)) responseTokens = responseCount;
            }
            return new AICompletionResult(text, model, promptTokens, responseTokens, latencyMilliseconds);
        }
        catch (JsonException ex)
        {
            throw new AIProviderException(ProviderName, "OpenAI returned a malformed generation response.", AIErrorCodes.MalformedResponse, ex);
        }
    }
}
