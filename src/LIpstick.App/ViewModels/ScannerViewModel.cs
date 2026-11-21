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
    private string _profileName = "Alex Vance";

    [ObservableProperty]
    private string _profileHeadline = "Senior Product Manager at Acme Corp | Passionate about building enterprise tech";

    [ObservableProperty]
    private int _totalScore = 42;

    [ObservableProperty]
    private string _scoreVerdict = "Needs Attention";

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
            var profile = await _linkedInClient.IngestProfileAsync();
            ProfileName = profile.FullName;
            ProfileHeadline = profile.Headline;

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
        finally
        {
            IsScanning = false;
        }
    }
}
