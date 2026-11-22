using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Infrastructure.LinkedIn;

namespace LIpstick_App.ViewModels;

public partial class ConnectViewModel : ObservableObject
{
    private readonly ILinkedInClient _linkedInClient;
    private readonly BrowserLinkedInProfileClient _profileClient;
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
    private string _linkedInPdfPath = "";

    [ObservableProperty]
    private string _linkedInPdfStatus = "No LinkedIn profile PDF selected.";

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
        BrowserLinkedInProfileClient profileClient,
        ISecureStorageService secureStorage,
        ShellViewModel shellViewModel,
        IAIHubService aiHub,
        ILocalDataService dataService)
    {
        _linkedInClient = linkedInClient;
        _profileClient = profileClient;
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
        var config = await _dataService.LoadConfigAsync();
        LinkedInProfileUrl = config.LinkedInProfileUrl ?? "";
        LinkedInPdfPath = config.LinkedInPdfPath ?? "";
        LinkedInPdfStatus = string.IsNullOrWhiteSpace(config.LinkedInPdfText)
            ? "No LinkedIn profile PDF selected. Required for the initial scan."
            : $"PDF ready: {Path.GetFileName(LinkedInPdfPath)}";
        IsLinkedInConnected = session.State == LinkedInConnectionState.Connected;
        LinkedInStatusMessage = session.StatusMessage ?? (IsLinkedInConnected ? "Connected" : "Not connected.");
        if (IsLinkedInConnected)
        {
                LinkedInUser = session.FullName ?? "Profile URL saved";
                LinkedInHeadline = session.Headline ?? "Use CHECK to verify the public URL and import your LinkedIn PDF.";
        }

        IsGeminiConnected = await _secureStorage.HasSecretAsync("GEMINI_API_KEY");
        GeminiStatusMessage = IsGeminiConnected ? "Connected - Gemini Flash / 2.5 active" : "Not connected. Enter API key to activate Gemini.";

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
    public async Task CheckLinkedInProfileAsync()
    {
        var value = LinkedInProfileUrl.Trim();
        if (!BrowserLinkedInProfileClient.IsProfileUrl(value))
        {
            LinkedInStatusMessage = "Enter a profile URL like https://www.linkedin.com/in/your-name/.";
            return;
        }

        var config = await _dataService.LoadConfigAsync();
        bool urlChanged = !string.Equals(config.LinkedInProfileUrl, value, StringComparison.OrdinalIgnoreCase);
        await _dataService.SaveConfigAsync(config with
        {
            LinkedInProfileUrl = value,
            LinkedInVerifiedProfileUrl = null,
            LinkedInPdfPath = urlChanged ? null : config.LinkedInPdfPath,
            LinkedInPdfText = urlChanged ? null : config.LinkedInPdfText,
            LinkedInProfileText = urlChanged ? null : config.LinkedInProfileText,
            LinkedInInitialProfile = urlChanged ? null : config.LinkedInInitialProfile,
            LinkedInLatestProfile = urlChanged ? null : config.LinkedInLatestProfile,
            LinkedInInitialScore = urlChanged ? null : config.LinkedInInitialScore,
            LinkedInLastScanScore = urlChanged ? null : config.LinkedInLastScanScore,
            LinkedInLastScannedUtc = urlChanged ? null : config.LinkedInLastScannedUtc
        });
        LinkedInProfileUrl = value;
        if (urlChanged)
        {
            LinkedInPdfPath = string.Empty;
            LinkedInPdfStatus = "Profile URL changed. Choose the matching LinkedIn PDF.";
        }
        LinkedInStatusMessage = "Checking public profile access…";
        try
        {
            var profile = await _profileClient.CheckPublicProfileAsync(value);
            var checkedConfig = await _dataService.LoadConfigAsync();
            await _dataService.SaveConfigAsync(checkedConfig with { LinkedInVerifiedProfileUrl = value });
            IsLinkedInConnected = true;
            LinkedInUser = profile.FullName;
            LinkedInHeadline = profile.Headline;
            LinkedInStatusMessage = "Public profile is accessible. Import the LinkedIn Save to PDF file, then run your initial scan.";
        }
        catch (Exception ex)
        {
            await _dataService.SaveConfigAsync((await _dataService.LoadConfigAsync()) with { LinkedInVerifiedProfileUrl = null });
            IsLinkedInConnected = false;
            LinkedInStatusMessage = ex.Message;
        }
        await _shellViewModel.InitializeAsync();
    }

    public async Task ImportLinkedInPdfAsync(string filePath)
    {
        LinkedInPdfStatus = "Reading LinkedIn PDF…";
        try
        {
            var imported = LinkedInProfilePdfImporter.ExtractText(filePath);
            var config = await _dataService.LoadConfigAsync();
            await _dataService.SaveConfigAsync(config with
            {
                LinkedInPdfPath = filePath,
                LinkedInPdfText = imported.Text,
                LinkedInInitialProfile = null,
                LinkedInLatestProfile = null,
                LinkedInProfileText = null,
                LinkedInInitialScore = null,
                LinkedInLastScanScore = null,
                LinkedInLastScannedUtc = null
            });
            LinkedInPdfPath = filePath;
            LinkedInPdfStatus = $"PDF ready ({imported.PageCount} pages). Initial scan will combine it with the checked public profile.";
        }
        catch (Exception ex)
        {
            LinkedInPdfStatus = ex.Message;
        }
    }

    [RelayCommand]
    public async Task SaveGeminiKeyAsync()
    {
        await SaveAndValidateProviderKeyAsync("GEMINI_API_KEY", GeminiApiKey, "gemini-flash-latest", "Gemini");
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
            if (providerName == "Gemini")
            {
                var config = await _dataService.LoadConfigAsync();
                if (config.SelectedModel is null or "gemini-2.0-flash")
                    await _dataService.SaveConfigAsync(config with { SelectedProvider = "Google Gemini", SelectedModel = modelId });
            }
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
            if (ex is AIProviderException { ErrorCode: AIErrorCodes.Timeout or AIErrorCodes.NetworkError or AIErrorCodes.InvalidEndpoint } typedError)
                status = $"Could not verify {providerName} key ({typedError.ErrorCode}): {GetSafeDiagnostic(typedError, candidate)}";
            if (!string.IsNullOrEmpty(previousKey))
                status += " The previous key was restored.";
            SetProviderStatus(providerName, !string.IsNullOrEmpty(previousKey), status);
        }
    }

    private static string GetSafeDiagnostic(Exception exception, string apiKey)
    {
        var cause = exception.GetBaseException();
        string message = cause.Message.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
        if (message.Length > 180) message = message[..180];
        return string.IsNullOrWhiteSpace(message) ? cause.GetType().Name : message;
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
