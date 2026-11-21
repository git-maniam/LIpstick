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
    private readonly ILinkedInSyncWorker _syncWorker;
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
        ILinkedInSyncWorker syncWorker,
        ILocalDataService dataService,
        IAIHubService? aiHub = null)
    {
        _linkedInClient = linkedInClient;
        _humanizer = humanizer;
        _syncWorker = syncWorker;
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
        StatusMessage = string.Empty;

        switch (section.ToLowerInvariant())
        {
            case "headline":
                AsIsText = profile.Headline;
                ToBeText = string.Empty;
                break;
            case "about":
                AsIsText = profile.About;
                ToBeText = string.Empty;
                break;
            case "experience":
                AsIsText = profile.Experiences.Count > 0 ? $"{profile.Experiences[0].Title} — {profile.Experiences[0].Company}\n{profile.Experiences[0].Description}" : string.Empty;
                ToBeText = string.Empty;
                break;
            case "featured":
                AsIsText = string.Join("\n\n", profile.FeaturedItems.Select(item => $"{item.Title}\n{item.Description}"));
                ToBeText = string.Empty;
                break;
            default:
                AsIsText = string.Empty;
                ToBeText = string.Empty;
                break;
        }

        UpdateScoreAndDiff();
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

    [RelayCommand]
    public async Task ApplyAsync()
    {
        StatusMessage = "Applying changes to profile...";
        var result = SelectedSection.ToLowerInvariant() switch
        {
            "headline" => await _syncWorker.UpdateHeadlineAsync(ToBeText),
            "about" => await _syncWorker.UpdateAboutAsync(ToBeText),
            "featured" => new UpdateResult(SelectedSection, UpdateOutcome.Unsupported, "Featured section updates are not supported by the current LinkedIn permissions.", DateTime.UtcNow, false),
            _ => await _syncWorker.UpdateHeadlineAsync(ToBeText)
        };

        if (result.Outcome == UpdateOutcome.Succeeded)
        {
            StatusMessage = $"✓ {SelectedSection} successfully updated on LinkedIn! Backed up to History.";
        }
        else if (result.Outcome == UpdateOutcome.Unsupported)
        {
            StatusMessage = result.Message ?? "Copy the reviewed text into LinkedIn manually, then scan an updated profile snapshot.";
        }
        else
        {
            StatusMessage = $"✗ Apply failed: {result.Message}";
        }
    }

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
        StatusMessage = "Re-generating recommendation with AI...";
        try
        {
            if (_aiHub != null)
            {
                var config = await _dataService.LoadConfigAsync();
                string model = config.SelectedModel ?? "gemini-2.0-flash";
                var req = new RecommendationRequest(
                    SelectedSection,
                    AsIsText,
                    "Direct, outcome-driven, professional, human",
                    new Dictionary<string, object>(),
                    model);

                var completion = await _aiHub.GenerateCompletionAsync(req);
                LastCompletion = completion;
                var generated = completion.Text;
                if (!string.IsNullOrWhiteSpace(generated))
                {
                    ToBeText = generated.Trim();
                    StatusMessage = "✓ Recommendation updated with fresh AI draft.";
                }
            }
            else
            {
                CleanAndHumanize();
                StatusMessage = "✓ Applied humanizer clean and de-slop.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Re-generate failed: {ex.Message}";
        }
    }
}
