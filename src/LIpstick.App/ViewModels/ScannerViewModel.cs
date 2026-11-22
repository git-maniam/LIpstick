using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Infrastructure.LinkedIn;

namespace LIpstick_App.ViewModels;

public partial class ScannerViewModel : ObservableObject
{
    private readonly BrowserLinkedInProfileClient _profileClient;
    private readonly IRubricEvaluator _evaluator;
    private readonly ILocalDataService _dataService;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanButtonLabel = "Initial scan";

    [ObservableProperty]
    private string _scoreComparison = "First scan uses your checked public URL and LinkedIn PDF to establish a baseline.";

    [ObservableProperty]
    private string _profileName = "LinkedIn profile";

    [ObservableProperty]
    private string _profileHeadline = "Connect LinkedIn to review your current profile.";

    [ObservableProperty]
    private int _totalScore;

    [ObservableProperty]
    private string _scoreVerdict = "Not scanned";

    [ObservableProperty]
    private string _statusMessage = "Connect LinkedIn to scan your profile.";

    [ObservableProperty]
    private List<RubricCategoryScore> _categoryScores = new();

    [ObservableProperty]
    private List<RubricCategoryScore> _fixFirstItems = new();

    public ScannerViewModel(BrowserLinkedInProfileClient profileClient, IRubricEvaluator evaluator, ILocalDataService dataService)
    {
        _profileClient = profileClient;
        _evaluator = evaluator;
        _dataService = dataService;
    }

    public async Task InitializeAsync()
    {
        var config = await _dataService.LoadConfigAsync();
        ScanButtonLabel = config.LinkedInInitialProfile is null ? "Initial scan" : "Re-scan public profile";
        if (config.LinkedInLatestProfile is null)
        {
            StatusMessage = "Save and check your public profile URL, import the LinkedIn PDF, then run the initial scan.";
            ScoreComparison = "First scan uses your checked public URL and LinkedIn PDF to establish a baseline.";
            return;
        }

        ShowEvaluation(config.LinkedInLatestProfile, config.LinkedInLastScanScore ?? 0);
        if (config.LinkedInInitialScore is int initialScore && config.LinkedInLastScanScore is int latestScore)
        {
            int delta = latestScore - initialScore;
            ScoreComparison = delta switch
            {
                > 0 => $"Up {delta} points since your initial scan ({initialScore} → {latestScore}).",
                < 0 => $"Down {-delta} points since your initial scan ({initialScore} → {latestScore}).",
                _ => $"Same score as your initial scan ({initialScore}/100)."
            };
        }
        StatusMessage = $"Last scanned {config.LinkedInLastScannedUtc?.ToLocalTime().ToString("g") ?? "previously"}. Click Re-scan after updating your LinkedIn profile.";
    }

    [RelayCommand]
    public async Task ScanProfileAsync()
    {
        IsScanning = true;
        try
        {
            var config = await _dataService.LoadConfigAsync();
            bool isInitial = config.LinkedInInitialProfile is null;
            if (!BrowserLinkedInProfileClient.IsProfileUrl(config.LinkedInProfileUrl))
                throw new InvalidOperationException("Save a valid LinkedIn profile URL in Connections first.");
            if (config.LinkedInVerifiedProfileUrl != config.LinkedInProfileUrl)
                throw new InvalidOperationException("Click CHECK in Connections to verify that the public profile is accessible.");
            if (isInitial && string.IsNullOrWhiteSpace(config.LinkedInPdfText))
                throw new InvalidOperationException("Import the LinkedIn Save to PDF file before your initial scan.");

            var profile = await _profileClient.ScanPublicProfileAsync(
                config.LinkedInProfileUrl!, isInitial ? config.LinkedInPdfText : null, isInitial);
            var evaluation = _evaluator.EvaluateProfile(profile);
            var latestConfig = await _dataService.LoadConfigAsync();
            int? initialScore = latestConfig.LinkedInInitialScore;
            if (isInitial) initialScore = evaluation.TotalScore;
            await _dataService.SaveConfigAsync(latestConfig with
            {
                LinkedInInitialProfile = isInitial ? profile : latestConfig.LinkedInInitialProfile,
                LinkedInInitialScore = initialScore,
                LinkedInLastScanScore = evaluation.TotalScore,
                LinkedInLastScannedUtc = DateTime.UtcNow,
                LinkedInSuggestions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            });

            ShowEvaluation(profile, evaluation.TotalScore);
            ScanButtonLabel = "Re-scan public profile";
            int scoreDelta = evaluation.TotalScore - (initialScore ?? evaluation.TotalScore);
            ScoreComparison = isInitial
                ? $"Initial baseline established at {evaluation.TotalScore}/100. Suggestions use your public profile and LinkedIn PDF."
                : scoreDelta switch
                {
                    > 0 => $"Profile improved by {scoreDelta} points since the initial scan ({initialScore} → {evaluation.TotalScore}).",
                    < 0 => $"Profile score decreased by {-scoreDelta} points since the initial scan ({initialScore} → {evaluation.TotalScore}). Review the updated sections and suggestions.",
                    _ => $"Profile score is unchanged at {evaluation.TotalScore}/100 since the initial scan."
                };
            StatusMessage = profile.SourceStatus ?? "Public profile re-scanned.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void ShowEvaluation(LinkedInProfile profile, int score)
    {
        ProfileName = profile.FullName;
        ProfileHeadline = profile.Headline;
        TotalScore = score;
        ScoreVerdict = score switch
        {
            >= 85 => "Rock Solid",
            >= 70 => "Strong Presence",
            >= 50 => "Average (Room for Growth)",
            _ => "Needs Attention"
        };
        var evaluation = _evaluator.EvaluateProfile(profile);
        CategoryScores = evaluation.Categories.ToList();
        FixFirstItems = evaluation.FixFirstCategories.ToList();
    }
}
