using System.Text.Json;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Infrastructure.Storage;

public class LocalDataService : ILocalDataService
{
    public string DataDirectory { get; }
    private readonly string _configFile;
    private readonly string _historyFile;
    private readonly string _voiceFile;
    private readonly string _planFile;
    private readonly string _bookmarkFile;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public LocalDataService(string? customDataDir = null)
    {
        if (!string.IsNullOrWhiteSpace(customDataDir))
        {
            DataDirectory = customDataDir;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            DataDirectory = Path.Combine(appData, "LIpstick");
        }

        Directory.CreateDirectory(DataDirectory);
        _configFile = Path.Combine(DataDirectory, "config.json");
        _historyFile = Path.Combine(DataDirectory, "history.json");
        _voiceFile = Path.Combine(DataDirectory, "voice.md");
        _planFile = Path.Combine(DataDirectory, "plan.md");
        _bookmarkFile = Path.Combine(DataDirectory, "bookmark.json");
    }

    public async Task<AppConfig> LoadConfigAsync()
    {
        await _ioLock.WaitAsync();
        try
        {
            if (!File.Exists(_configFile))
            {
                var defaultConfig = new AppConfig { DataDirectory = DataDirectory };
                await SaveAtomicJsonInternalAsync(_configFile, defaultConfig);
                return defaultConfig;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_configFile);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                return config ?? new AppConfig { DataDirectory = DataDirectory };
            }
            catch (Exception)
            {
                // ST-01: Corrupt file recovery without crashing
                BackupCorruptFile(_configFile);
                var recoveredConfig = new AppConfig { DataDirectory = DataDirectory };
                await SaveAtomicJsonInternalAsync(_configFile, recoveredConfig);
                return recoveredConfig;
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        await _ioLock.WaitAsync();
        try
        {
            await SaveAtomicJsonInternalAsync(_configFile, config with { DataDirectory = DataDirectory, LastUpdated = DateTime.UtcNow });
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<HistoryLog> LoadHistoryAsync()
    {
        await _ioLock.WaitAsync();
        try
        {
            if (!File.Exists(_historyFile))
            {
                var initialLog = new HistoryLog(1, Array.Empty<ProfileHistoryItem>());
                await SaveAtomicJsonInternalAsync(_historyFile, initialLog);
                return initialLog;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_historyFile);
                var log = JsonSerializer.Deserialize<HistoryLog>(json, JsonOptions);
                return log ?? new HistoryLog(1, Array.Empty<ProfileHistoryItem>());
            }
            catch (Exception)
            {
                // ST-01: Corrupt history recovery
                BackupCorruptFile(_historyFile);
                var recoveredLog = new HistoryLog(1, Array.Empty<ProfileHistoryItem>());
                await SaveAtomicJsonInternalAsync(_historyFile, recoveredLog);
                return recoveredLog;
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task AppendHistoryAsync(ProfileHistoryItem item)
    {
        await _ioLock.WaitAsync();
        try
        {
            var log = await LoadHistoryInternalAsync();
            var updatedList = new List<ProfileHistoryItem>(log.Items) { item };
            var updatedLog = new HistoryLog(log.Version + 1, updatedList);
            await SaveAtomicJsonInternalAsync(_historyFile, updatedLog);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<string> LoadVoiceProfileAsync()
    {
        await _ioLock.WaitAsync();
        try
        {
            if (!File.Exists(_voiceFile))
            {
                const string defaultVoice = "# User Voice Profile\n\nTone: Direct, evidence-driven, pragmatic.\nPoint of View: Experienced builder solving real-world friction.\nKey Proof: Concrete metrics, outcome numbers, verified frameworks.\n";
                await File.WriteAllTextAsync(_voiceFile, defaultVoice);
                return defaultVoice;
            }

            try
            {
                return await File.ReadAllTextAsync(_voiceFile);
            }
            catch
            {
                BackupCorruptFile(_voiceFile);
                return string.Empty;
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task SaveVoiceProfileAsync(string voiceMarkdown)
    {
        await _ioLock.WaitAsync();
        try
        {
            var tmp = _voiceFile + ".tmp";
            await File.WriteAllTextAsync(tmp, voiceMarkdown);
            File.Move(tmp, _voiceFile, overwrite: true);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<string> LoadContentPlanAsync()
    {
        await _ioLock.WaitAsync();
        try
        {
            if (!File.Exists(_planFile))
            {
                const string defaultPlan = "# 4x/Week Publishing Strategy\n\n- Mon: Proof / Case Study\n- Tue: Contrarian Observation\n- Thu: Actionable Framework\n- Fri: Personal Retrospective\n";
                await File.WriteAllTextAsync(_planFile, defaultPlan);
                return defaultPlan;
            }

            try
            {
                return await File.ReadAllTextAsync(_planFile);
            }
            catch
            {
                BackupCorruptFile(_planFile);
                return string.Empty;
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task SaveContentPlanAsync(string planMarkdown)
    {
        await _ioLock.WaitAsync();
        try
        {
            var tmp = _planFile + ".tmp";
            await File.WriteAllTextAsync(tmp, planMarkdown);
            File.Move(tmp, _planFile, overwrite: true);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> ClearLocalDataAsync()
    {
        await _ioLock.WaitAsync();
        try
        {
            var deleted = new List<string>();
            var targets = new[] { _configFile, _historyFile, _voiceFile, _planFile, _bookmarkFile, Path.Combine(DataDirectory, "secure_vault.dat") };

            foreach (var target in targets)
            {
                if (File.Exists(target))
                {
                    File.Delete(target);
                    deleted.Add(Path.GetFileName(target));
                }
            }

            // Also remove any backup files
            foreach (var bak in Directory.GetFiles(DataDirectory, "*.bak"))
            {
                File.Delete(bak);
                deleted.Add(Path.GetFileName(bak));
            }

            return deleted;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task SaveRestartBookmarkAsync(SessionBookmark bookmark, CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            await SaveAtomicJsonInternalAsync(_bookmarkFile, bookmark);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<SessionBookmark?> GetRestartBookmarkAsync(CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_bookmarkFile))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_bookmarkFile, cancellationToken);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<SessionBookmark>(json, JsonOptions);
            }
            catch (Exception)
            {
                // Safe parsing fallback: corrupted or truncated bookmark returns null without crashing app startup
                BackupCorruptFile(_bookmarkFile);
                return null;
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task ClearRestartBookmarkAsync(CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_bookmarkFile))
            {
                File.Delete(_bookmarkFile);
            }

            var tmp = _bookmarkFile + ".tmp";
            if (File.Exists(tmp))
            {
                File.Delete(tmp);
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<HistoryLog> LoadHistoryInternalAsync()
    {
        if (!File.Exists(_historyFile))
            return new HistoryLog(1, Array.Empty<ProfileHistoryItem>());

        try
        {
            var json = await File.ReadAllTextAsync(_historyFile);
            return JsonSerializer.Deserialize<HistoryLog>(json, JsonOptions) ?? new HistoryLog(1, Array.Empty<ProfileHistoryItem>());
        }
        catch
        {
            return new HistoryLog(1, Array.Empty<ProfileHistoryItem>());
        }
    }

    private static async Task SaveAtomicJsonInternalAsync<T>(string targetPath, T value)
    {
        var tempPath = targetPath + ".tmp";
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    private static void BackupCorruptFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var bakPath = filePath + ".corrupt.bak";
                File.Copy(filePath, bakPath, overwrite: true);
            }
        }
        catch { }
    }
}
