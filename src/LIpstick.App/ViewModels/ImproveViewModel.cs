using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Core.Services;

namespace LIpstick_App.ViewModels;

public partial class ImproveViewModel : ObservableObject
{
    private readonly ILinkedInClient _linkedInClient;
    private readonly IHumanizerEngine _humanizer;
    private readonly ILocalDataService _dataService;
    private readonly IAIHubService? _aiHub;

    [ObservableProperty]
    private string _selectedSection = "Headline";

    [ObservableProperty]
    private string _asIsText = "";

    [ObservableProperty]
    private string _toBeText = "";

    [ObservableProperty]
    private double _overallHumanScore = 88.0;

    [ObservableProperty]
    private string _verdictPillText = "88 PASS";

    [ObservableProperty]
    private double _burstiness = 78.0;

    [ObservableProperty]
    private double _specificity = 92.0;

    [ObservableProperty]
    private double _slop = 100.0;

    [ObservableProperty]
    private double _fingerprint = 100.0;

    [ObservableProperty]
    private double _voice = 82.0;

    [ObservableProperty]
    private bool _isPass;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenerationMetadata))]
    private AICompletionResult? _lastCompletion;

    public string GenerationMetadata => LastCompletion is null
        ? string.Empty
        : $"{LastCompletion.ModelId} • {LastCompletion.LatencyMilliseconds} ms • input {LastCompletion.PromptTokenCount?.ToString() ?? "n/a"} tokens • output {LastCompletion.ResponseTokenCount?.ToString() ?? "n/a"} tokens";

    [ObservableProperty]
    private List<DiffChunk> _diffChunks = new();

    public ImproveViewModel(
        ILinkedInClient linkedInClient,
        IHumanizerEngine humanizer,
        ILocalDataService dataService,
        IAIHubService? aiHub = null)
    {
        _linkedInClient = linkedInClient;
        _humanizer = humanizer;
        _dataService = dataService;
        _aiHub = aiHub;
    }

    public async Task InitializeAsync()
    {
        await LoadSectionAsync(SelectedSection);
    }

    [RelayCommand]
    public async Task SelectSectionAsync(string section)
    {
        SelectedSection = section;
        await LoadSectionAsync(section);
    }

    private async Task LoadSectionAsync(string section)
    {
        var session = await _linkedInClient.GetSessionInfoAsync();
        if (session.State != LinkedInConnectionState.Connected)
        {
            AsIsText = string.Empty;
            ToBeText = string.Empty;
            StatusMessage = "Connect LinkedIn to load your profile. You can draft after connecting an AI provider.";
            UpdateScoreAndDiff();
            return;
        }

        var profile = await _linkedInClient.IngestProfileAsync();
        var config = await _dataService.LoadConfigAsync();

        switch (section.ToLowerInvariant())
        {
            case "headline":
                AsIsText = profile.Headline;
                ToBeText = config.LinkedInSuggestions.GetValueOrDefault("Headline", string.Empty);
                break;
            case "about":
                AsIsText = profile.About;
                ToBeText = config.LinkedInSuggestions.GetValueOrDefault("About", string.Empty);
                break;
            case "experience":
                AsIsText = profile.Experiences.Count > 0 ? $"{profile.Experiences[0].Title} — {profile.Experiences[0].Company}\n{profile.Experiences[0].Description}" : string.Empty;
                ToBeText = config.LinkedInSuggestions.GetValueOrDefault("Experience", string.Empty);
                break;
            case "featured":
                AsIsText = string.Join("\n\n", profile.FeaturedItems.Select(item => $"{item.Title}\n{item.Description}"));
                ToBeText = config.LinkedInSuggestions.GetValueOrDefault("Featured", string.Empty);
                break;
            default:
                AsIsText = string.Empty;
                ToBeText = string.Empty;
                break;
        }

        UpdateScoreAndDiff();
        if (string.IsNullOrWhiteSpace(ToBeText))
        {
            if (string.IsNullOrWhiteSpace(AsIsText))
                StatusMessage = $"The latest scan contains no readable {section} content to improve. Check the profile and PDF, then scan again.";
            else if (_aiHub is null)
                StatusMessage = "Connect an AI provider to generate a profile suggestion.";
            else
                await ReGenerateAsync();
        }
        else
            StatusMessage = "Saved suggestion loaded. Edit it, regenerate it, or copy it to LinkedIn manually.";
    }

    partial void OnToBeTextChanged(string value)
    {
        UpdateScoreAndDiff();
    }

    private void UpdateScoreAndDiff()
    {
        var eval = _humanizer.Evaluate(ToBeText);
        OverallHumanScore = eval.OverallScore;
        Burstiness = eval.BurstinessScore;
        Specificity = eval.SpecificityScore;
        Slop = eval.SlopDensityScore;
        Fingerprint = eval.FingerprintScore;
        Voice = eval.VoiceScore;

        IsPass = eval.Verdict == HumanizerVerdict.Pass;
        VerdictPillText = $"{OverallHumanScore:F0} {eval.Verdict.ToString().ToUpperInvariant()}";

        DiffChunks = DiffEngine.ComputeWordDiff(AsIsText, ToBeText).ToList();
    }

    public void NotifySuggestionCopied() =>
        StatusMessage = $"{SelectedSection} suggestion copied. Apply it manually in LinkedIn, then return to Scanner and select Re-scan.";

    [RelayCommand]
    public void CleanAndHumanize()
    {
        var (cleaned, _) = _humanizer.CleanText(ToBeText);
        ToBeText = cleaned;
        UpdateScoreAndDiff();
    }

    [RelayCommand]
    public async Task ReGenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(AsIsText))
        {
            StatusMessage = $"The latest scan contains no readable {SelectedSection} content to improve.";
            return;
        }

        StatusMessage = "Re-generating recommendation with AI...";
        try
        {
            if (_aiHub != null)
            {
                var config = await _dataService.LoadConfigAsync();
                string model = config.SelectedModel ?? "gemini-flash-latest";
                var req = new RecommendationRequest(
                    SelectedSection,
                    AsIsText,
                    "Direct, outcome-driven, professional, human. Preserve facts and claims from the source. Never invent employers, dates, metrics, credentials, or achievements.",
                    new Dictionary<string, object>(),
                    model);

                var completion = await _aiHub.GenerateCompletionAsync(req);
                LastCompletion = completion;
                var generated = completion.Text;
                if (!string.IsNullOrWhiteSpace(generated))
                {
                    ToBeText = generated.Trim();
                    var suggestions = new Dictionary<string, string>(config.LinkedInSuggestions, StringComparer.OrdinalIgnoreCase)
                    {
                        [SelectedSection] = ToBeText
                    };
                    await _dataService.SaveConfigAsync(config with { LinkedInSuggestions = suggestions });
                    StatusMessage = "AI suggestion generated. Review it, then copy and apply it manually in LinkedIn.";
                }
            }
            else
            {
                StatusMessage = "Connect an AI provider to generate a profile suggestion.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Re-generate failed: {ex.Message}";
        }
    }
}
