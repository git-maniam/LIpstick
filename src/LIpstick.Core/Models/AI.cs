namespace LIpstick.Core.Models;

public record RecommendationRequest(
    string SectionName,
    string CurrentText,
    string VoiceProfile,
    IReadOnlyDictionary<string, object> RubricCriteria,
    string ModelName
);

public record ModelInfo(
    string Id,
    string DisplayName,
    string ProviderName,
    int ContextWindowTokens,
    int EstimatedLatencyMs,
    bool IsConnected
)
{
    public string FormattedCapacity => ContextWindowTokens >= 1_000_000
        ? $"{ContextWindowTokens / 1_000_000.0:0.#}M tokens"
        : $"{ContextWindowTokens / 1_000}k tokens";

    public string MetadataSummary => $"{ProviderName} • {FormattedCapacity} • ~{EstimatedLatencyMs}ms";

    public override string ToString() => $"{DisplayName} ({MetadataSummary})";
}

public record AICompletionResult(
    string Text,
    string ModelId,
    int? PromptTokenCount,
    int? ResponseTokenCount,
    int LatencyMilliseconds);

public static class AIErrorCodes
{
    public const string NoApiKey = "NO_API_KEY";
    public const string InvalidKey = "INVALID_KEY";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    public const string QuotaExceeded = "RESOURCE_EXHAUSTED";
    public const string Timeout = "TIMEOUT";
    public const string NetworkError = "NETWORK_ERROR";
    public const string MalformedResponse = "MALFORMED_RESPONSE";
}

public class AIProviderException : Exception
{
    public string Provider { get; }
    public string? ErrorCode { get; }

    public AIProviderException(string provider, string message, string? errorCode = null, Exception? inner = null)
        : base($"[{provider}] {message}", inner)
    {
        Provider = provider;
        ErrorCode = errorCode;
    }
}
