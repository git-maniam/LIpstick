using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick_App.ViewModels;

public partial class ScannerViewModel : ObservableObject
{
    private readonly ILinkedInClient _linkedInClient;
    private readonly IRubricEvaluator _evaluator;

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

    public ScannerViewModel(ILinkedInClient linkedInClient, IRubricEvaluator evaluator)
    {
        _linkedInClient = linkedInClient;
        _evaluator = evaluator;
    }

    public async Task InitializeAsync()
    {
        await ScanProfileAsync();
    }

    [RelayCommand]
    public async Task ScanProfileAsync()
    {
        IsScanning = true;
        try
        {
            var session = await _linkedInClient.GetSessionInfoAsync();
            if (session.State != LinkedInConnectionState.Connected)
            {
                StatusMessage = "Connect LinkedIn from Connections before scanning a profile.";
                ProfileName = "LinkedIn profile";
                ProfileHeadline = "Your profile details will appear here after connecting.";
                TotalScore = 0;
                ScoreVerdict = "Not scanned";
                CategoryScores = new();
                FixFirstItems = new();
                return;
            }

            var profile = await _linkedInClient.IngestProfileAsync();
            ProfileName = profile.FullName;
            ProfileHeadline = profile.Headline;
            StatusMessage = string.Empty;

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
