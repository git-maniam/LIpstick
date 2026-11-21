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
    private List<DiffChunk> _diffChunks = new();

    public ImproveViewModel(
        ILinkedInClient linkedInClient,
        IHumanizerEngine humanizer,
        ILinkedInSyncWorker syncWorker,
        ILocalDataService dataService)
    {
        _linkedInClient = linkedInClient;
        _humanizer = humanizer;
        _syncWorker = syncWorker;
        _dataService = dataService;
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
        var profile = await _linkedInClient.IngestProfileAsync();

        switch (section.ToLowerInvariant())
        {
            case "headline":
                AsIsText = profile.Headline;
                ToBeText = "Building AI agents that save B2B teams 10h/wk | $4.2M pipeline generated | Ops @ Acme";
                break;
            case "about":
                AsIsText = profile.About;
                ToBeText = @"Most ops leaders lose 12 hours every week wrestling fragmented tools.
I build deterministic automations that cut that manual grind to zero.

Over the last 4 years at Acme and Beta Corp:
- Automated 45 core business workflows across 6 departments
- Reclaimed 14,000 engineering hours
- Scaled data pipelines from 10k to 2.5M daily events with 99.98% uptime

If you want to streamline your team's workflow, send a DM or email me at alex@example.com.";
                break;
            case "experience":
                AsIsText = profile.Experiences.Count > 0 ? profile.Experiences[0].Description : "";
                ToBeText = @"Led cross-functional team of 8 engineers delivering enterprise data sync:
- Reduced sync latency from 45 minutes to 3.2 seconds across 12M records
- Cut AWS infrastructure costs by $180,000 annually through query optimization
- Onboarded 14 enterprise pilot customers with zero SLA breaches";
                break;
            default:
                AsIsText = profile.Headline;
                ToBeText = "Building AI solutions with verifiable outcomes.";
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
            _ => await _syncWorker.UpdateHeadlineAsync(ToBeText)
        };

        if (result.Outcome == UpdateOutcome.Succeeded)
        {
            StatusMessage = $"✓ {SelectedSection} successfully updated on LinkedIn! Backed up to History.";
        }
        else if (result.Outcome == UpdateOutcome.Unsupported)
        {
            StatusMessage = "ℹ Copied to clipboard! LinkedIn edit page opened in browser.";
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
}
