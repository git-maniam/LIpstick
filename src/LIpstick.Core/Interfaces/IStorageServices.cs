using LIpstick.Core.Models;

namespace LIpstick.Core.Interfaces;

public interface ISecureStorageService
{
    Task StoreSecretAsync(string key, string secret);
    Task<string?> GetSecretAsync(string key);
    Task DeleteSecretAsync(string key);
    Task<bool> HasSecretAsync(string key);
}

public interface ILocalDataService
{
    string DataDirectory { get; }
    Task<AppConfig> LoadConfigAsync();
    Task SaveConfigAsync(AppConfig config);
    Task<HistoryLog> LoadHistoryAsync();
    Task AppendHistoryAsync(ProfileHistoryItem item);
    Task<string> LoadVoiceProfileAsync();
    Task SaveVoiceProfileAsync(string voiceMarkdown);
    Task<string> LoadContentPlanAsync();
    Task SaveContentPlanAsync(string planMarkdown);
    Task<IReadOnlyList<string>> ClearLocalDataAsync();
    Task SaveRestartBookmarkAsync(SessionBookmark bookmark, CancellationToken cancellationToken = default);
    Task<SessionBookmark?> GetRestartBookmarkAsync(CancellationToken cancellationToken = default);
    Task ClearRestartBookmarkAsync(CancellationToken cancellationToken = default);
}
