using System.Text.Json.Serialization;

namespace LIpstick.Core.Models;

public record AppConfig
{
    public string Theme { get; init; } = "Light"; // Light, Dark, System
    public string? SelectedProvider { get; init; } = "Google Gemini";
    public string? SelectedModel { get; init; } = "gemini-2.0-flash";
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
