using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Infrastructure.LinkedIn;

namespace LIpstick_App.ViewModels;

public partial class ScannerViewModel : ObservableObject
{
    private readonly ILinkedInClient _linkedInClient;
    private readonly IRubricEvaluator _evaluator;
    private readonly ILocalDataService _dataService;

    [ObservableProperty]
    private string _profileTextInput = string.Empty;

    [ObservableProperty]
    private bool _usePastedSnapshot;

    [ObservableProperty]
    private bool _isScanning;

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

    public ScannerViewModel(ILinkedInClient linkedInClient, IRubricEvaluator evaluator, ILocalDataService dataService)
    {
        _linkedInClient = linkedInClient;
        _evaluator = evaluator;
        _dataService = dataService;
    }

    public async Task InitializeAsync()
    {
        var config = await _dataService.LoadConfigAsync();
        ProfileTextInput = config.LinkedInProfileText ?? string.Empty;
        await ScanProfileAsync();
    }

    [RelayCommand]
    public async Task ScanProfileAsync()
    {
        IsScanning = true;
        try
        {
            LinkedInProfile profile;
            if (UsePastedSnapshot)
            {
                var config = await _dataService.LoadConfigAsync();
                if (!BrowserLinkedInProfileClient.IsProfileUrl(config.LinkedInProfileUrl))
                    throw new InvalidOperationException("Save a valid LinkedIn profile URL in Connections first.");
                if (string.IsNullOrWhiteSpace(ProfileTextInput))
                    throw new InvalidOperationException("Paste the profile text before choosing pasted-snapshot scan.");
                await _dataService.SaveConfigAsync(config with { LinkedInProfileText = ProfileTextInput.Trim() });
                profile = BrowserLinkedInProfileClient.ParseProfile(config.LinkedInProfileUrl!, ProfileTextInput.Trim()) with
                {
                    SourceStatus = "Scanned the profile text pasted by you."
                };
            }
            else
            {
                var session = await _linkedInClient.GetSessionInfoAsync();
                if (session.State != LinkedInConnectionState.Connected)
                {
                    StatusMessage = "Open LinkedIn from Connections and save your profile URL before scanning.";
                    ProfileName = "LinkedIn profile";
                    ProfileHeadline = "Your profile details will appear here after setting a profile URL.";
                    TotalScore = 0;
                    ScoreVerdict = "Not scanned";
                    CategoryScores = new();
                    FixFirstItems = new();
                    return;
                }
                profile = await _linkedInClient.IngestProfileAsync();
            }
            if (!string.IsNullOrWhiteSpace(profile.SourceSnapshotText))
                ProfileTextInput = profile.SourceSnapshotText;
            ProfileName = profile.FullName;
            ProfileHeadline = profile.Headline;
            StatusMessage = profile.SourceStatus ?? string.Empty;

            var evaluation = _evaluator.EvaluateProfile(profile);
            TotalScore = evaluation.TotalScore;
            ScoreVerdict = TotalScore switch
            {
                >= 85 => "Rock Solid",
                >= 70 => "Strong Presence",
                >= 50 => "Average (Room for Growth)",
                _ => "Needs Attention"
            };

            CategoryScores = evaluation.Categories.ToList();
            FixFirstItems = evaluation.FixFirstCategories.ToList();
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
}
