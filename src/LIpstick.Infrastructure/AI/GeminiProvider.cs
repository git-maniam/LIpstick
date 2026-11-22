using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;
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
            new("gemini-flash-latest", "Gemini Flash (Latest)", ProviderName, 1000000, 350, true),
            new("gemini-2.5-flash", "Gemini 2.5 Flash (Fast)", ProviderName, 1000000, 350, true),
            new("gemini-2.0-flash", "Gemini 2.0 Flash (Default)", ProviderName, 1000000, 400, true),
            new("gemini-1.5-pro", "Gemini 1.5 Pro (Deep Reasoning)", ProviderName, 2000000, 850, true)
        };
    }

    public async Task<string> GenerateRecommendationAsync(RecommendationRequest request, CancellationToken cancellationToken = default) =>
        (await GenerateCompletionAsync(request, cancellationToken)).Text;

    public async Task<AICompletionResult> GenerateCompletionAsync(RecommendationRequest request, CancellationToken cancellationToken = default)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var apiKey = await _secureStorage.GetSecretAsync("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AIProviderException(ProviderName, "Gemini API key is not configured.", AIErrorCodes.NoApiKey);
        }

        string model = string.IsNullOrWhiteSpace(request.ModelName) ? "gemini-flash-latest" : request.ModelName;
        string baseUrl = !string.IsNullOrWhiteSpace(CustomBaseUrl)
            ? CustomBaseUrl
            : (request.RubricCriteria?.TryGetValue("CustomBaseUrl", out var customUrl) == true ? customUrl.ToString()! : "https://generativelanguage.googleapis.com");
        var endpoint = NormalizeEndpoint(baseUrl);

        var prompt = PromptContextAssembler.AssemblePrompt(request);

        try
        {
            var client = new Client(
                apiKey: apiKey,
                httpOptions: new HttpOptions
                {
                    BaseUrl = endpoint.BaseUrl,
                    ApiVersion = endpoint.ApiVersion
                },
                clientOptions: new ClientOptions { HttpClientFactory = () => _httpClient });
            var response = await client.Models.GenerateContentAsync(
                model,
                prompt,
                new GenerateContentConfig { Temperature = 0.4f, MaxOutputTokens = 800 },
                cancellationToken);
            timer.Stop();
            string text = response.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                throw new AIProviderException(ProviderName, "Gemini returned no generated text.", AIErrorCodes.MalformedResponse);

            int? promptTokens = response.UsageMetadata?.PromptTokenCount;
            int? responseTokens = response.UsageMetadata?.CandidatesTokenCount;
            return new AICompletionResult(text, model, promptTokens, responseTokens, (int)timer.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new AIProviderException(ProviderName, "Gemini request timed out.", AIErrorCodes.Timeout, ex);
        }
        catch (ClientError ex)
        {
            string message = ex.Message.ToLowerInvariant();
            string code = message.Contains("429") || message.Contains("rate") ? AIErrorCodes.RateLimitExceeded
                : message.Contains("quota") || message.Contains("resource_exhausted") || message.Contains("403") ? AIErrorCodes.QuotaExceeded
                : message.Contains("401") || message.Contains("unauthorized") || message.Contains("api key") || message.Contains("invalid key") ? AIErrorCodes.InvalidKey
                : message.Contains("404") || message.Contains("not found") || message.Contains("invalid url") ? AIErrorCodes.InvalidEndpoint
                : AIErrorCodes.NetworkError;
            throw new AIProviderException(ProviderName, "Gemini request failed. Check credentials, quota, and endpoint settings.", code, ex);
        }
        catch (ApiException ex)
        {
            string message = ex.Message.ToLowerInvariant();
            string code = message.Contains("429") || message.Contains("rate") ? AIErrorCodes.RateLimitExceeded
                : message.Contains("quota") || message.Contains("resource_exhausted") || message.Contains("403") ? AIErrorCodes.QuotaExceeded
                : message.Contains("401") || message.Contains("unauthorized") || message.Contains("api key") || message.Contains("invalid key") ? AIErrorCodes.InvalidKey
                : message.Contains("404") || message.Contains("not found") || message.Contains("invalid url") ? AIErrorCodes.InvalidEndpoint
                : AIErrorCodes.NetworkError;
            throw new AIProviderException(ProviderName, "Gemini service request failed.", code, ex);
        }
        catch (HttpRequestException ex)
        {
            var rootCause = ex.GetBaseException();
            string code = rootCause is TimeoutException or System.Net.WebException { Status: System.Net.WebExceptionStatus.Timeout }
                ? AIErrorCodes.Timeout
                : AIErrorCodes.NetworkError;
            throw new AIProviderException(ProviderName, "Network or protocol error calling Gemini API.", code, ex);
        }
        catch (Exception ex) when (ex is not AIProviderException)
        {
            throw new AIProviderException(ProviderName, "Network or protocol error calling Gemini API.", AIErrorCodes.NetworkError, ex);
        }
    }

    internal static (string BaseUrl, string? ApiVersion) NormalizeEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("Enter a valid Gemini API endpoint URL.");

        string path = uri.AbsolutePath.TrimEnd('/');
        var fullGenerateEndpoint = System.Text.RegularExpressions.Regex.Match(
            path,
            @"^/(?<version>v\d+(?:beta\d*)?)/models/[^/]+:generateContent$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (fullGenerateEndpoint.Success)
            return (uri.GetLeftPart(UriPartial.Authority), fullGenerateEndpoint.Groups["version"].Value);

        var versionedBase = System.Text.RegularExpressions.Regex.Match(
            path,
            @"^/(?<version>v\d+(?:beta\d*)?)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (versionedBase.Success)
            return (uri.GetLeftPart(UriPartial.Authority), versionedBase.Groups["version"].Value);

        return (endpoint.TrimEnd('/'), null);
    }

}
