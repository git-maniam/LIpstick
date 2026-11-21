using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick_App.ViewModels;

public partial class ConnectViewModel : ObservableObject
{
    private readonly ILinkedInClient _linkedInClient;
    private readonly ISecureStorageService _secureStorage;
    private readonly ShellViewModel _shellViewModel;

    [ObservableProperty]
    private string _linkedInUser = "Alex Vance";

    [ObservableProperty]
    private string _linkedInHeadline = "Senior Product Manager at Acme Corp";

    [ObservableProperty]
    private bool _isLinkedInConnected = true;

    [ObservableProperty]
    private string _geminiApiKey = "";

    [ObservableProperty]
    private bool _isGeminiConnected;

    [ObservableProperty]
    private string _geminiStatusMessage = "Not connected. Enter API key to activate Gemini.";

    [ObservableProperty]
    private string _openAiApiKey = "";

    [ObservableProperty]
    private bool _isOpenAiConnected;

    [ObservableProperty]
    private string _openAiStatusMessage = "Not connected. Enter API key to activate OpenAI.";

    public ConnectViewModel(
        ILinkedInClient linkedInClient,
        ISecureStorageService secureStorage,
        ShellViewModel shellViewModel)
    {
        _linkedInClient = linkedInClient;
        _secureStorage = secureStorage;
        _shellViewModel = shellViewModel;
    }

    public async Task InitializeAsync()
    {
        var session = await _linkedInClient.GetSessionInfoAsync();
        IsLinkedInConnected = session.State == LinkedInConnectionState.Connected;
        if (IsLinkedInConnected)
        {
            LinkedInUser = session.FullName ?? "Connected Member";
            LinkedInHeadline = session.Headline ?? "Profile connected";
        }

        IsGeminiConnected = await _secureStorage.HasSecretAsync("GEMINI_API_KEY");
        GeminiStatusMessage = IsGeminiConnected ? "Connected - Gemini 2.0 / 2.5 active" : "Not connected. Enter API key to activate Gemini.";

        IsOpenAiConnected = await _secureStorage.HasSecretAsync("OPENAI_API_KEY");
        OpenAiStatusMessage = IsOpenAiConnected ? "Connected - GPT-4o / o3-mini active" : "Not connected. Enter API key to activate OpenAI.";
    }

    [RelayCommand]
    public async Task ToggleLinkedInConnectionAsync()
    {
        if (IsLinkedInConnected)
        {
            await _linkedInClient.DisconnectAsync();
            IsLinkedInConnected = false;
        }
        else
        {
            // Reconnect
            if (_linkedInClient is LIpstick.Infrastructure.LinkedIn.MockLinkedInClient mock)
            {
                mock.CurrentState = LinkedInConnectionState.Connected;
            }
            IsLinkedInConnected = true;
        }
        await _shellViewModel.InitializeAsync();
    }

    [RelayCommand]
    public async Task SaveGeminiKeyAsync()
    {
        if (!string.IsNullOrWhiteSpace(GeminiApiKey))
        {
            await _secureStorage.StoreSecretAsync("GEMINI_API_KEY", GeminiApiKey.Trim());
            GeminiApiKey = "";
            IsGeminiConnected = true;
            GeminiStatusMessage = "Connected successfully. Models loaded.";
            await _shellViewModel.InitializeAsync();
        }
    }

    [RelayCommand]
    public async Task DisconnectGeminiAsync()
    {
        await _secureStorage.DeleteSecretAsync("GEMINI_API_KEY");
        IsGeminiConnected = false;
        GeminiStatusMessage = "Disconnected.";
        await _shellViewModel.InitializeAsync();
    }

    [RelayCommand]
    public async Task SaveOpenAiKeyAsync()
    {
        if (!string.IsNullOrWhiteSpace(OpenAiApiKey))
        {
            await _secureStorage.StoreSecretAsync("OPENAI_API_KEY", OpenAiApiKey.Trim());
            OpenAiApiKey = "";
            IsOpenAiConnected = true;
            OpenAiStatusMessage = "Connected successfully. Models loaded.";
            await _shellViewModel.InitializeAsync();
        }
    }

    [RelayCommand]
    public async Task DisconnectOpenAiAsync()
    {
        await _secureStorage.DeleteSecretAsync("OPENAI_API_KEY");
        IsOpenAiConnected = false;
        OpenAiStatusMessage = "Disconnected.";
        await _shellViewModel.InitializeAsync();
    }
}
