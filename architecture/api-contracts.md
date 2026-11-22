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
    Task<LinkedInSessionInfo> ConnectAsync(CancellationToken cancellationToken = default); // Opens default browser login shortcut only.
    Task<LinkedInSessionInfo> GetSessionInfoAsync(CancellationToken cancellationToken = default); // Reports saved profile setup, not browser auth.
    Task<LinkedInProfile> IngestProfileAsync(CancellationToken cancellationToken = default); // Returns the latest saved scan.
}
```
- Connections persists a public member URL and provides a **CHECK** action that makes one unauthenticated request to verify readable content.
- The initial scan requires a locally selected LinkedIn Save to PDF file, fetches the checked public URL, merges the available sources, and saves the initial profile/score baseline.
- Each later Re-scan fetches the public URL again and evaluates fresh public content only; it retains the initial baseline for comparison and does not reuse the PDF.
- No LinkedIn password, cookie, or browser token is accessed. No access-control bypass or profile write calls are used. Recommendations are applied manually by the user.
