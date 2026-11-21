using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Infrastructure.AI;

namespace LIpstick_App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalDataService _dataService;
    private readonly ShellViewModel _shellViewModel;
    private readonly GeminiProvider? _geminiProvider;
    private readonly OpenAIProvider? _openAIProvider;

    [ObservableProperty]
    private string _currentTheme = "Light";

    [ObservableProperty]
    private string _dataDirectory = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _geminiCustomBaseUrl = "";

    [ObservableProperty]
    private string _openAiCustomBaseUrl = "";

    [ObservableProperty]
    private string _selectedModel = "gemini-2.0-flash";

    public IReadOnlyList<ModelInfo> AvailableModels => _shellViewModel.AvailableModels;

    public SettingsViewModel(
        ILocalDataService dataService,
        ShellViewModel shellViewModel,
        GeminiProvider? geminiProvider = null,
        OpenAIProvider? openAIProvider = null)
    {
        _dataService = dataService;
        _shellViewModel = shellViewModel;
        _geminiProvider = geminiProvider;
        _openAIProvider = openAIProvider;
        DataDirectory = _dataService.DataDirectory;
    }

    public async Task InitializeAsync()
    {
        var config = await _dataService.LoadConfigAsync();
        CurrentTheme = config.Theme;
        if (!string.IsNullOrEmpty(config.SelectedModel))
        {
            SelectedModel = config.SelectedModel;
        }

        GeminiCustomBaseUrl = config.GeminiCustomBaseUrl ?? "";
        OpenAiCustomBaseUrl = config.OpenAiCustomBaseUrl ?? "";
        if (_geminiProvider != null)
        {
            _geminiProvider.CustomBaseUrl = config.GeminiCustomBaseUrl;
        }
        if (_openAIProvider != null)
        {
            _openAIProvider.CustomBaseUrl = config.OpenAiCustomBaseUrl;
        }

        if (_geminiProvider?.CustomBaseUrl != null)
        {
            GeminiCustomBaseUrl = _geminiProvider.CustomBaseUrl;
        }

        if (_openAIProvider?.CustomBaseUrl != null)
        {
            OpenAiCustomBaseUrl = _openAIProvider.CustomBaseUrl;
        }
    }

    [RelayCommand]
    public async Task SetThemeAsync(string theme)
    {
        CurrentTheme = theme;
        await _shellViewModel.ChangeThemeAsync(theme);
    }

    [RelayCommand]
    public async Task SaveEndpointsAsync()
    {
        if (!IsValidEndpoint(GeminiCustomBaseUrl) || !IsValidEndpoint(OpenAiCustomBaseUrl))
        {
            StatusMessage = "Enter a valid HTTP or HTTPS base URL, or leave the field blank.";
            return;
        }

        var config = await _dataService.LoadConfigAsync();
        config = config with
        {
            GeminiCustomBaseUrl = NullIfWhiteSpace(GeminiCustomBaseUrl),
            OpenAiCustomBaseUrl = NullIfWhiteSpace(OpenAiCustomBaseUrl)
        };
        await _dataService.SaveConfigAsync(config);

        if (_geminiProvider != null)
        {
            _geminiProvider.CustomBaseUrl = config.GeminiCustomBaseUrl;
        }

        if (_openAIProvider != null)
        {
            _openAIProvider.CustomBaseUrl = config.OpenAiCustomBaseUrl;
        }

        if (!string.IsNullOrEmpty(SelectedModel))
        {
            await _shellViewModel.SelectModelAsync(SelectedModel);
        }

        StatusMessage = "✓ AI configuration and endpoints updated successfully.";
    }

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');

    private static bool IsValidEndpoint(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
         (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
         string.IsNullOrEmpty(uri.UserInfo));

    [RelayCommand]
    public async Task ClearLocalDataAsync()
    {
        var deleted = await _dataService.ClearLocalDataAsync();
        StatusMessage = $"✓ Removed {deleted.Count} local artifacts: {string.Join(", ", deleted)}";
        await _shellViewModel.InitializeAsync();
    }
}
