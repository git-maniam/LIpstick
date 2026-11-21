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

## 3. LinkedIn Write-Back Contract
```csharp
namespace LIpstick.Core.Interfaces;

public interface ILinkedInSyncWorker
{
    Task<bool> UpdateHeadlineAsync(string newHeadline, CancellationToken cancellationToken = default);
    Task<bool> UpdateAboutAsync(string newAbout, CancellationToken cancellationToken = default);
    Task<bool> UpdateExperienceAsync(string experienceId, string newDescription, CancellationToken cancellationToken = default);
}
```
