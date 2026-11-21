using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick_App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly ILinkedInClient _linkedInClient;
    private readonly ISecureStorageService _secureStorage;
    private readonly ILocalDataService _dataService;

    private static readonly SolidColorBrush ConnectedBrush = new(ColorHelper.FromArgb(255, 16, 185, 129));
    private static readonly SolidColorBrush DisconnectedBrush = new(ColorHelper.FromArgb(255, 239, 68, 68));

    public SolidColorBrush LinkedInStatusBrush => IsLinkedInConnected ? ConnectedBrush : DisconnectedBrush;
    public SolidColorBrush GeminiStatusBrush => IsGeminiConnected ? ConnectedBrush : DisconnectedBrush;
    public SolidColorBrush OpenAiStatusBrush => IsOpenAiConnected ? ConnectedBrush : DisconnectedBrush;

    [ObservableProperty]
    private string _linkedInStatus = "Disconnected";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LinkedInStatusBrush))]
    private bool _isLinkedInConnected;

    [ObservableProperty]
    private string _geminiStatus = "Disconnected";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GeminiStatusBrush))]
    private bool _isGeminiConnected;

    [ObservableProperty]
    private string _openAiStatus = "Disconnected";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenAiStatusBrush))]
    private bool _isOpenAiConnected;

    [ObservableProperty]
    private List<string> _availableModels = new();

    [ObservableProperty]
    private string? _selectedModel;

    [ObservableProperty]
    private string _currentTheme = "Light";

    public ShellViewModel(
        ILinkedInClient linkedInClient,
        ISecureStorageService secureStorage,
        ILocalDataService dataService)
    {
        _linkedInClient = linkedInClient;
        _secureStorage = secureStorage;
        _dataService = dataService;
    }

    public async Task InitializeAsync()
    {
        // 1. Check LinkedIn session
        var session = await _linkedInClient.GetSessionInfoAsync();
        IsLinkedInConnected = session.State == LinkedInConnectionState.Connected;
        LinkedInStatus = IsLinkedInConnected ? "Connected" : "Disconnected";

        // 2. Check AI keys
        IsGeminiConnected = await _secureStorage.HasSecretAsync("GEMINI_API_KEY");
        GeminiStatus = IsGeminiConnected ? "Connected" : "Disconnected";

        IsOpenAiConnected = await _secureStorage.HasSecretAsync("OPENAI_API_KEY");
        OpenAiStatus = IsOpenAiConnected ? "Connected" : "Disconnected";

        // 3. Populate model selector
        UpdateAvailableModels();

        // 4. Load config theme
        var config = await _dataService.LoadConfigAsync();
        CurrentTheme = config.Theme;
        if (!string.IsNullOrEmpty(config.SelectedModel) && AvailableModels.Contains(config.SelectedModel))
        {
            SelectedModel = config.SelectedModel;
        }
    }

    public void UpdateAvailableModels()
    {
        var models = new List<string>();

        if (IsGeminiConnected)
        {
            models.Add("gemini-2.0-flash (Default)");
            models.Add("gemini-2.5-flash");
            models.Add("gemini-1.5-pro");
        }

        if (IsOpenAiConnected)
        {
            models.Add("gpt-4o");
            models.Add("gpt-4o-mini");
            models.Add("o3-mini");
        }

        if (models.Count == 0)
        {
            models.Add("Connect an AI provider below");
        }

        AvailableModels = models;
        if (string.IsNullOrEmpty(SelectedModel) || !models.Contains(SelectedModel))
        {
            SelectedModel = models[0];
        }
    }

    [RelayCommand]
    public async Task ChangeThemeAsync(string newTheme)
    {
        CurrentTheme = newTheme;
        var config = await _dataService.LoadConfigAsync();
        await _dataService.SaveConfigAsync(config with { Theme = newTheme });
    }

    [RelayCommand]
    public async Task SelectModelAsync(string model)
    {
        SelectedModel = model;
        var config = await _dataService.LoadConfigAsync();
        await _dataService.SaveConfigAsync(config with { SelectedModel = model });
    }
}
