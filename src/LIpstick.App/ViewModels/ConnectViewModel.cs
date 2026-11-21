using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Infrastructure.LinkedIn;

namespace LIpstick_App.ViewModels;

public partial class ConnectViewModel : ObservableObject
{
    private readonly ILinkedInClient _linkedInClient;
    private readonly ISecureStorageService _secureStorage;
    private readonly ShellViewModel _shellViewModel;
    private readonly IAIHubService _aiHub;
    private readonly ILocalDataService _dataService;

    [ObservableProperty]
    private string _linkedInUser = "Not connected";

    [ObservableProperty]
    private string _linkedInHeadline = "Connect your profile to scan and optimize.";

    [ObservableProperty]
    private bool _isLinkedInConnected;

    [ObservableProperty]
    private string _linkedInStatusMessage = "Sign in through your browser and save your profile URL.";

    [ObservableProperty]
    private string _linkedInProfileUrl = "";

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
        IAIHubService aiHub,
        ILocalDataService dataService)
    {
        _linkedInClient = linkedInClient;
        _secureStorage = secureStorage;
        _shellViewModel = shellViewModel;
        _aiHub = aiHub;
        _dataService = dataService;
    }

    public async Task InitializeAsync()
    {
        // Remove credentials from the retired developer-app flow; LIpstick no longer uses them.
        foreach (var key in new[]
        {
            "LINKEDIN_CLIENT_ID", "LINKEDIN_CLIENT_SECRET", "LINKEDIN_ACCESS_TOKEN",
            "LINKEDIN_REFRESH_TOKEN", "LINKEDIN_MEMBER_ID", "LINKEDIN_TOKEN_EXPIRY",
            "LINKEDIN_FULL_NAME", "LINKEDIN_HEADLINE", "LINKEDIN_AVATAR_URL"
        })
            await _secureStorage.DeleteSecretAsync(key);

        var session = await _linkedInClient.GetSessionInfoAsync();
        LinkedInProfileUrl = (await _dataService.LoadConfigAsync()).LinkedInProfileUrl ?? "";
        IsLinkedInConnected = session.State == LinkedInConnectionState.Connected;
        LinkedInStatusMessage = session.StatusMessage ?? (IsLinkedInConnected ? "Connected" : "Not connected.");
        if (IsLinkedInConnected)
        {
                LinkedInUser = session.FullName ?? "Profile URL saved";
                LinkedInHeadline = session.Headline ?? "Use your browser login, then provide a profile snapshot in Scanner.";
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
            var session = await _linkedInClient.ConnectAsync();
            IsLinkedInConnected = session.State == LinkedInConnectionState.Connected;
            LinkedInStatusMessage = session.StatusMessage ?? "LinkedIn opened in your default browser.";
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
    public async Task SaveLinkedInProfileUrlAsync()
    {
        var value = LinkedInProfileUrl.Trim();
        if (!BrowserLinkedInProfileClient.IsProfileUrl(value))
        {
            LinkedInStatusMessage = "Enter a profile URL like https://www.linkedin.com/in/your-name/.";
            return;
        }

        var config = await _dataService.LoadConfigAsync();
        await _dataService.SaveConfigAsync(config with { LinkedInProfileUrl = value });
        LinkedInProfileUrl = value;
        IsLinkedInConnected = true;
        LinkedInUser = "Profile URL saved";
        LinkedInStatusMessage = "Profile URL saved. Paste your profile text in Scanner to create a scan snapshot.";
        await _shellViewModel.InitializeAsync();
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
