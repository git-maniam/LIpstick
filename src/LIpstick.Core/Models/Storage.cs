using System.Text.Json.Serialization;

namespace LIpstick.Core.Models;

public record AppConfig
{
    public string Theme { get; init; } = "Light"; // Light, Dark, System
    public string? SelectedProvider { get; init; } = "Google Gemini";
    public string? SelectedModel { get; init; } = "gemini-2.0-flash";
    public string? GeminiCustomBaseUrl { get; init; }
    public string? OpenAiCustomBaseUrl { get; init; }
    public bool TelemetryEnabled { get; init; } = false;
    public string DataDirectory { get; init; } = "";
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}

public record ProfileHistoryItem(
    string Id,
    string SectionName,
    string PreviousText,
    string AppliedText,
    double HumanScore,
    string Status, // Applied, RolledBack
    DateTime Timestamp,
    string AppliedModel
)
{
    [JsonIgnore]
    public string FormattedTimestamp => Timestamp.ToString("g");

    [JsonIgnore]
    public string FormattedHumanScore => HumanScore.ToString("F0");
}

public record HistoryLog(
    int Version,
    IReadOnlyList<ProfileHistoryItem> Items
);

public record SessionBookmark
{
    public string ActivePageRoute { get; init; } = string.Empty;
    public string? ActiveSection { get; init; }
    public string? LinkedInSessionState { get; init; }
    public string? ProfileSnapshot { get; init; }
    public string? UnsavedDraftText { get; init; }
    public string? ActiveModelId { get; init; }
    public DateTime SavedAtUtc { get; init; } = DateTime.UtcNow;

    public SessionBookmark() { }

    public SessionBookmark(
        string activePageRoute,
        string? activeSection = null,
        string? linkedInSessionState = null,
        string? profileSnapshot = null,
        string? unsavedDraftText = null,
        string? activeModelId = null,
        DateTime? savedAtUtc = null)
    {
        ActivePageRoute = activePageRoute;
        ActiveSection = activeSection;
        LinkedInSessionState = linkedInSessionState;
        ProfileSnapshot = profileSnapshot;
        UnsavedDraftText = unsavedDraftText;
        ActiveModelId = activeModelId;
        SavedAtUtc = savedAtUtc ?? DateTime.UtcNow;
    }
}

