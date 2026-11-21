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
    private readonly IAIHubService _aiHub;

    [ObservableProperty]
    private string _linkedInUser = "Not connected";

    [ObservableProperty]
    private string _linkedInHeadline = "Connect your profile to scan and optimize.";

    [ObservableProperty]
    private bool _isLinkedInConnected;

    [ObservableProperty]
    private string _linkedInStatusMessage = "Configure your LinkedIn developer app to connect.";

    [ObservableProperty]
    private string _linkedInClientId = "";

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
        ShellViewModel shellViewModel,
        IAIHubService aiHub)
    {
        _linkedInClient = linkedInClient;
        _secureStorage = secureStorage;
        _shellViewModel = shellViewModel;
        _aiHub = aiHub;
    }

    public async Task InitializeAsync()
    {
        var session = await _linkedInClient.GetSessionInfoAsync();
        IsLinkedInConnected = session.State == LinkedInConnectionState.Connected;
        LinkedInStatusMessage = session.StatusMessage ?? (IsLinkedInConnected ? "Connected" : "Not connected.");
        if (IsLinkedInConnected)
        {
            LinkedInUser = session.FullName ?? "Connected Member";
            LinkedInHeadline = session.Headline ?? "Profile identity connected; headline is unavailable with the granted LinkedIn permissions.";
        }

        IsGeminiConnected = await _secureStorage.HasSecretAsync("GEMINI_API_KEY");
        GeminiStatusMessage = IsGeminiConnected ? "Connected - Gemini 2.0 / 2.5 active" : "Not connected. Enter API key to activate Gemini.";

        IsOpenAiConnected = await _secureStorage.HasSecretAsync("OPENAI_API_KEY");
        OpenAiStatusMessage = IsOpenAiConnected ? "Connected - GPT-4o / o3-mini active" : "Not connected. Enter API key to activate OpenAI.";
    }

    [RelayCommand]
    public async Task ToggleLinkedInConnectionAsync()
    {
        try
        {
            if (IsLinkedInConnected)
            {
                await _linkedInClient.DisconnectAsync();
                IsLinkedInConnected = false;
                LinkedInUser = "Disconnected";
                LinkedInHeadline = "Connect your profile to scan and optimize.";
            }
            else
            {
                var session = await _linkedInClient.ConnectAsync();
                IsLinkedInConnected = session.State == LinkedInConnectionState.Connected;
                LinkedInStatusMessage = session.StatusMessage ?? (IsLinkedInConnected ? "Connected" : "Not connected.");
                if (IsLinkedInConnected)
                {
                    LinkedInUser = session.FullName ?? "Connected Member";
                    LinkedInHeadline = session.Headline ?? "Profile identity connected; headline is unavailable with the granted LinkedIn permissions.";
                }
            }
            await _shellViewModel.InitializeAsync();
        }
        catch (OperationCanceledException)
        {
            IsLinkedInConnected = false;
            LinkedInStatusMessage = "Sign-in was cancelled.";
            LinkedInUser = "Disconnected";
            LinkedInHeadline = "Connection was cancelled.";
        }
        catch (Exception ex)
        {
            IsLinkedInConnected = false;
            LinkedInStatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    public async Task SaveLinkedInCredentialsAsync()
    {
        if (string.IsNullOrWhiteSpace(LinkedInClientId))
        {
            LinkedInStatusMessage = "Enter the LinkedIn Client ID.";
            return;
        }

        await _secureStorage.StoreSecretAsync("LINKEDIN_CLIENT_ID", LinkedInClientId.Trim());
        await _secureStorage.DeleteSecretAsync("LINKEDIN_CLIENT_SECRET");
        LinkedInStatusMessage = "Client ID saved. Request native OAuth PKCE enablement for your LinkedIn app before connecting.";
    }

    [RelayCommand]
    public async Task SaveGeminiKeyAsync()
    {
        await SaveAndValidateProviderKeyAsync("GEMINI_API_KEY", GeminiApiKey, "gemini-2.0-flash", "Gemini");
        GeminiApiKey = "";
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
        await SaveAndValidateProviderKeyAsync("OPENAI_API_KEY", OpenAiApiKey, "gpt-4o-mini", "OpenAI");
        OpenAiApiKey = "";
    }

    private async Task SaveAndValidateProviderKeyAsync(string storageKey, string candidate, string modelId, string providerName)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            SetProviderStatus(providerName, false, $"Enter a {providerName} API key.");
            return;
        }

        string? previousKey = await _secureStorage.GetSecretAsync(storageKey);
        await _secureStorage.StoreSecretAsync(storageKey, candidate.Trim());
        try
        {
            var validationRequest = new RecommendationRequest(
                "credential validation",
                "Reply with OK.",
                string.Empty,
                new Dictionary<string, object>(),
                modelId);
            await _aiHub.GenerateCompletionAsync(validationRequest);
            SetProviderStatus(providerName, true, "Connected and verified.");
            await _shellViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            if (string.IsNullOrEmpty(previousKey))
                await _secureStorage.DeleteSecretAsync(storageKey);
            else
                await _secureStorage.StoreSecretAsync(storageKey, previousKey);

            string status = ex is AIProviderException providerError
                ? $"Could not verify {providerName} key ({providerError.ErrorCode ?? "provider error"}). Check the key, quota, and endpoint."
                : $"Could not verify {providerName} key. Check network access and endpoint settings.";
            if (!string.IsNullOrEmpty(previousKey))
                status += " The previous key was restored.";
            SetProviderStatus(providerName, !string.IsNullOrEmpty(previousKey), status);
        }
    }

    private void SetProviderStatus(string providerName, bool connected, string status)
    {
        if (providerName == "Gemini")
        {
            IsGeminiConnected = connected;
            GeminiStatusMessage = status;
        }
        else
        {
            IsOpenAiConnected = connected;
            OpenAiStatusMessage = status;
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
