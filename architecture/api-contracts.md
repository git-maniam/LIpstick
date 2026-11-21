# API Contracts & Data Flow Specifications (C# .NET 10)

## 1. AI Provider Interface
```csharp
namespace LIpstick.Core.Interfaces;

public record RecommendationRequest(
    string SectionName,
    string CurrentText,
    string VoiceProfile,
    IReadOnlyDictionary<string, object> RubricCriteria,
    string ModelName
);

public interface IAIProvider
{
    string ProviderName { get; }
    Task<IReadOnlyList<string>> ListAvailableModelsAsync(CancellationToken cancellationToken = default);
    Task<string> GenerateRecommendationAsync(RecommendationRequest request, CancellationToken cancellationToken = default);
}
```

## 2. Humanizer Scoring Contract
```csharp
namespace LIpstick.Core.Interfaces;

public enum HumanizerVerdict
{
    Pass,
    Review,
    Flagged
}

public record HumanizerResult(
    string CleanedText,
    double OverallScore,          // 0 to 100 (Pass >= 70)
    HumanizerVerdict Verdict,
    double BurstinessScore,
    double SpecificityScore,
    double SlopDensityScore,
    double FingerprintScore,
    double VoiceScore,
    IReadOnlyList<string> FlaggedStructuralTells
);

public interface IHumanizerEngine
{
    string CleanText(string input);
    HumanizerResult Evaluate(string text, string? referenceVoice = null);
}
```

## 3. LinkedIn Profile Snapshot Contract
```csharp
namespace LIpstick.Core.Interfaces;

public interface ILinkedInClient
{
    Task<LinkedInSessionInfo> ConnectAsync(CancellationToken cancellationToken = default); // Opens default browser login.
    Task<LinkedInSessionInfo> GetSessionInfoAsync(CancellationToken cancellationToken = default); // Reports profile setup, not browser auth.
    Task<LinkedInProfile> IngestProfileAsync(CancellationToken cancellationToken = default); // Parses user-provided local snapshot.
}
```
- LinkedIn passwords and browser sessions remain in the browser and are inaccessible to LIpstick.
- Each explicit scan makes one unauthenticated request to the saved public profile URL; no cookies or bulk requests are sent. Parsed text is cached locally; pasted text is the fallback if direct access is blocked or incomplete.
- No browser automation or profile write API calls are used. Recommendations are applied manually by the user.
