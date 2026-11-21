using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Infrastructure.AI;

namespace LIpstick_App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly ILinkedInClient _linkedInClient;
    private readonly ISecureStorageService _secureStorage;
    private readonly ILocalDataService _dataService;
    private readonly IAIHubService? _aiHub;
    private readonly GeminiProvider? _geminiProvider;
    private readonly OpenAIProvider? _openAIProvider;

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
    private IReadOnlyList<ModelInfo> _availableModels = Array.Empty<ModelInfo>();

    [ObservableProperty]
    private ModelInfo? _selectedModelInfo;

    [ObservableProperty]
    private string? _selectedModel;

    [ObservableProperty]
    private string _currentTheme = "Light";

    public ShellViewModel(
        ILinkedInClient linkedInClient,
        ISecureStorageService secureStorage,
        ILocalDataService dataService,
        IAIHubService? aiHub = null,
        GeminiProvider? geminiProvider = null,
        OpenAIProvider? openAIProvider = null)
    {
        _linkedInClient = linkedInClient;
        _secureStorage = secureStorage;
        _dataService = dataService;
        _aiHub = aiHub;
        _geminiProvider = geminiProvider;
        _openAIProvider = openAIProvider;
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
        await UpdateAvailableModelsAsync();

        // 4. Load config theme & model
        var config = await _dataService.LoadConfigAsync();
        CurrentTheme = config.Theme;
        if (_geminiProvider != null) _geminiProvider.CustomBaseUrl = config.GeminiCustomBaseUrl;
        if (_openAIProvider != null) _openAIProvider.CustomBaseUrl = config.OpenAiCustomBaseUrl;
        if (!string.IsNullOrEmpty(config.SelectedModel))
        {
            var match = AvailableModels.FirstOrDefault(m => m.Id == config.SelectedModel || m.DisplayName == config.SelectedModel);
            if (match != null)
            {
                SelectedModelInfo = match;
                SelectedModel = match.Id;
            }
        }
    }

    public void UpdateAvailableModels()
    {
        _ = UpdateAvailableModelsAsync();
    }

    public async Task UpdateAvailableModelsAsync()
    {
        var models = new List<ModelInfo>();

        if (_aiHub != null)
        {
            try
            {
                var hubModels = await _aiHub.GetAllAvailableModelsAsync();
                if (hubModels != null && hubModels.Count > 0)
                {
                    models.AddRange(hubModels);
                }
            }
            catch { }
        }

        if (models.Count == 0)
        {
            if (IsGeminiConnected)
            {
                models.Add(new ModelInfo("gemini-2.0-flash", "Gemini 2.0 Flash (Default)", "Google Gemini", 1000000, 400, true));
                models.Add(new ModelInfo("gemini-2.5-flash", "Gemini 2.5 Flash (Fast)", "Google Gemini", 1000000, 350, true));
                models.Add(new ModelInfo("gemini-1.5-pro", "Gemini 1.5 Pro (Deep Reasoning)", "Google Gemini", 2000000, 850, true));
            }

            if (IsOpenAiConnected)
            {
                models.Add(new ModelInfo("gpt-4o", "GPT-4o (Omni)", "OpenAI", 128000, 450, true));
                models.Add(new ModelInfo("gpt-4o-mini", "GPT-4o mini (Fast)", "OpenAI", 128000, 280, true));
                models.Add(new ModelInfo("o3-mini", "o3-mini (Reasoning)", "OpenAI", 200000, 950, true));
            }
        }

        if (models.Count == 0)
        {
            models.Add(new ModelInfo("none", "Connect an AI provider below", "None", 0, 0, false));
        }

        AvailableModels = models;
        if (SelectedModelInfo == null || !models.Any(m => m.Id == SelectedModelInfo.Id))
        {
            SelectedModelInfo = models[0];
            SelectedModel = SelectedModelInfo.Id;
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
        SelectedModelInfo = AvailableModels.FirstOrDefault(m => m.Id == model || m.DisplayName == model);
        var config = await _dataService.LoadConfigAsync();
        await _dataService.SaveConfigAsync(config with { SelectedModel = model });
    }
}
