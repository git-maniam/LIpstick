namespace LIpstick.Core.Models;

public enum HumanizerVerdict
{
    Pass,
    Review,
    Flagged
}

public record CheckScore(
    double Score,
    string Detail
);

public record InvisibleHit(
    string Name,
    int Count,
    string Action
);

public record TypoHit(
    string Name,
    int Count,
    string To
);

public record LexicalHit(
    string Find,
    string Replace,
    int Count,
    string Family
);

public record StructureFlag(
    string Name,
    int Count,
    string Fix
);

public record HumanizeReport(
    IReadOnlyList<InvisibleHit> Invisible,
    IReadOnlyList<TypoHit> Typographic,
    IReadOnlyList<LexicalHit> Lexical,
    IReadOnlyList<StructureFlag> Structures
)
{
    public int TotalArtifactsRemoved =>
        Invisible.Sum(h => h.Count) + Typographic.Sum(h => h.Count) + Lexical.Sum(h => h.Count);
}

public record HumanizerResult(
    string CleanedText,
    double OverallScore,
    HumanizerVerdict Verdict,
    double BurstinessScore,
    double SpecificityScore,
    double SlopDensityScore,
    double FingerprintScore,
    double VoiceScore,
    IReadOnlyList<string> FlaggedStructuralTells,
    HumanizeReport Report
);
