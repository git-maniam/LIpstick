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
);

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
