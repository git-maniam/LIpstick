using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LIpstick.Core.Interfaces;

namespace LIpstick.Infrastructure.Storage;

public class DpapiSecureStorageService : ISecureStorageService
{
    private readonly string _vaultFilePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private Dictionary<string, string>? _cache;

    public DpapiSecureStorageService(string? customVaultPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customVaultPath))
        {
            _vaultFilePath = customVaultPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "LIpstick");
            Directory.CreateDirectory(dir);
            _vaultFilePath = Path.Combine(dir, "secure_vault.dat");
        }
    }

    public async Task StoreSecretAsync(string key, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(secret);

        await _semaphore.WaitAsync();
        try
        {
            var map = await LoadMapInternalAsync();
            map[key] = secret;
            await SaveMapInternalAsync(map);
            _cache = map;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string?> GetSecretAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await _semaphore.WaitAsync();
        try
        {
            var map = await LoadMapInternalAsync();
            return map.TryGetValue(key, out var val) ? val : null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteSecretAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await _semaphore.WaitAsync();
        try
        {
            var map = await LoadMapInternalAsync();
            if (map.Remove(key))
            {
                await SaveMapInternalAsync(map);
                _cache = map;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> HasSecretAsync(string key)
    {
        var secret = await GetSecretAsync(key);
        return !string.IsNullOrEmpty(secret);
    }

    private async Task<Dictionary<string, string>> LoadMapInternalAsync()
    {
        if (_cache != null)
            return new Dictionary<string, string>(_cache);

        if (!File.Exists(_vaultFilePath))
            return new Dictionary<string, string>();

        try
        {
            var encryptedBytes = await File.ReadAllBytesAsync(_vaultFilePath);
            if (encryptedBytes.Length == 0)
                return new Dictionary<string, string>();

            byte[] decryptedBytes;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                // Fallback for non-Windows test environments
                decryptedBytes = encryptedBytes;
            }

            var json = Encoding.UTF8.GetString(decryptedBytes);
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return map ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private async Task SaveMapInternalAsync(Dictionary<string, string> map)
    {
        var json = JsonSerializer.Serialize(map);
        var plainBytes = Encoding.UTF8.GetBytes(json);

        byte[] cipherBytes;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        }
        else
        {
            cipherBytes = plainBytes;
        }

        var tempPath = _vaultFilePath + ".tmp";
        await File.WriteAllBytesAsync(tempPath, cipherBytes);
        File.Move(tempPath, _vaultFilePath, overwrite: true);
    }
}
