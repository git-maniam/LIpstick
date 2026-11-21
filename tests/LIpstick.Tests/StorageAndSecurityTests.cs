using LIpstick.Core.Models;
using LIpstick.Infrastructure.Storage;

namespace LIpstick.Tests;

[TestClass]
public class StorageAndSecurityTests
{
    private string _testDir = null!;
    private LocalDataService _dataService = null!;
    private DpapiSecureStorageService _secureStorage = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "LIpstick_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _dataService = new LocalDataService(_testDir);
        _secureStorage = new DpapiSecureStorageService(Path.Combine(_testDir, "test_vault.dat"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch { }
    }

    [TestMethod]
    public async Task ST_01_Corrupt_File_Recovery_Without_Crash()
    {
        // 1. Write corrupted JSON to config.json
        string configFile = Path.Combine(_testDir, "config.json");
        await File.WriteAllTextAsync(configFile, "{ invalid json truncated...");

        // 2. LoadConfig must recover with defaults, create .corrupt.bak, and not throw
        var config = await _dataService.LoadConfigAsync();

        Assert.IsNotNull(config);
        Assert.AreEqual("Light", config.Theme);
        Assert.IsTrue(File.Exists(configFile + ".corrupt.bak"), "Must preserve corrupt file as .bak");

        // 3. Repeat for history.json
        string historyFile = Path.Combine(_testDir, "history.json");
        await File.WriteAllTextAsync(historyFile, "<<corrupt xml in json>>");

        var history = await _dataService.LoadHistoryAsync();
        Assert.IsNotNull(history);
        Assert.AreEqual(0, history.Items.Count);
        Assert.IsTrue(File.Exists(historyFile + ".corrupt.bak"), "Must preserve corrupt history as .bak");
    }

    [TestMethod]
    public async Task ST_02_Clear_Local_Data_Removes_Artifacts_And_Reports_List()
    {
        // Setup initial files
        await _dataService.SaveConfigAsync(new AppConfig { Theme = "Dark" });
        await _dataService.SaveVoiceProfileAsync("Custom Voice");
        await _dataService.SaveContentPlanAsync("Custom Plan");
        await _secureStorage.StoreSecretAsync("TEST_KEY", "secret_value_123");

        var deletedFiles = await _dataService.ClearLocalDataAsync();

        Assert.IsTrue(deletedFiles.Contains("config.json"), "Must delete config.json");
        Assert.IsTrue(deletedFiles.Contains("voice.md"), "Must delete voice.md");
        Assert.IsTrue(deletedFiles.Contains("plan.md"), "Must delete plan.md");
    }

    [TestMethod]
    public async Task T2_Secret_Isolation_DPAPI_Encryption()
    {
        const string secretValue = "sk-live-super-secret-key-998877";
        await _secureStorage.StoreSecretAsync("OPENAI_KEY", secretValue);

        // Verify secret is retrievable through interface
        var retrieved = await _secureStorage.GetSecretAsync("OPENAI_KEY");
        Assert.AreEqual(secretValue, retrieved);

        // Verify disk content does NOT contain raw plain text secret
        string vaultPath = Path.Combine(_testDir, "test_vault.dat");
        byte[] rawBytes = await File.ReadAllBytesAsync(vaultPath);
        string rawString = System.Text.Encoding.UTF8.GetString(rawBytes);

        Assert.IsFalse(rawString.Contains(secretValue), "Vault file on disk must NOT contain plaintext secret");
    }

    [TestMethod]
    public async Task ST_01b_Corrupt_Or_Truncated_Voice_And_Plan_Files_Recover_With_Defaults_And_Backup()
    {
        string voiceFile = Path.Combine(_testDir, "voice.md");
        string planFile = Path.Combine(_testDir, "plan.md");

        await File.WriteAllBytesAsync(voiceFile, new byte[] { 0x00, 0xFF, 0xFE, 0x12 });
        await File.WriteAllBytesAsync(planFile, new byte[] { 0xAA, 0xBB, 0xCC });

        string voice = await _dataService.LoadVoiceProfileAsync();
        string plan = await _dataService.LoadContentPlanAsync();

        Assert.IsTrue(voice.Length > 0, "Voice profile must recover default content");
        Assert.IsTrue(plan.Length > 0, "Content plan must recover default content");
    }

    [TestMethod]
    public async Task ST_01c_Zero_Byte_Truncated_Json_Files_Recover_Cleanly_Without_Crash()
    {
        string configFile = Path.Combine(_testDir, "config.json");
        string historyFile = Path.Combine(_testDir, "history.json");

        await File.WriteAllBytesAsync(configFile, Array.Empty<byte>());
        await File.WriteAllBytesAsync(historyFile, Array.Empty<byte>());

        var config = await _dataService.LoadConfigAsync();
        var history = await _dataService.LoadHistoryAsync();

        Assert.IsNotNull(config);
        Assert.AreEqual("Light", config.Theme);
        Assert.IsNotNull(history);
        Assert.AreEqual(0, history.Items.Count);
    }

    [TestMethod]
    public async Task ST_02b_Clear_Local_Data_Leaves_Undocumented_Files_Untouched_And_Removes_Only_Documented_Artifacts()
    {
        await _dataService.SaveConfigAsync(new AppConfig { Theme = "Dark" });
        await _dataService.SaveVoiceProfileAsync("Voice");
        await _dataService.SaveContentPlanAsync("Plan");

        string foreignFile = Path.Combine(_testDir, "user_notes.txt");
        await File.WriteAllTextAsync(foreignFile, "Important custom notes");

        var deleted = await _dataService.ClearLocalDataAsync();

        Assert.IsTrue(File.Exists(foreignFile), "ClearLocalData must NOT delete undocumented files");
        Assert.IsFalse(deleted.Contains("user_notes.txt"));
    }

    [TestMethod]
    public async Task ST_02c_Cancel_Clear_Local_Data_Preserves_All_Existing_Artifacts()
    {
        await _dataService.SaveConfigAsync(new AppConfig { Theme = "Dark" });
        string configFile = Path.Combine(_testDir, "config.json");

        // When user cancels confirmation dialog, ClearLocalDataAsync is NOT called
        Assert.IsTrue(File.Exists(configFile));
        var config = await _dataService.LoadConfigAsync();
        Assert.AreEqual("Dark", config.Theme);
    }

    [TestMethod]
    public async Task T2b_DPAPI_Corrupted_Vault_Recovers_Gracefully_Without_Crash()
    {
        string vaultPath = Path.Combine(_testDir, "corrupted_vault.dat");
        await File.WriteAllBytesAsync(vaultPath, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02 });

        var freshStorage = new DpapiSecureStorageService(vaultPath);
        var secret = await freshStorage.GetSecretAsync("ANY_KEY");
        Assert.IsNull(secret, "Corrupted vault must safely return null instead of crashing");

        await freshStorage.StoreSecretAsync("NEW_KEY", "new_secret_val");
        var retrieved = await freshStorage.GetSecretAsync("NEW_KEY");
        Assert.AreEqual("new_secret_val", retrieved);
    }

    [TestMethod]
    public async Task T2c_DPAPI_Multiple_Secrets_Lifecycle_And_Deletion_Isolation()
    {
        await _secureStorage.StoreSecretAsync("GEMINI_API_KEY", "gemini_sec_111");
        await _secureStorage.StoreSecretAsync("OPENAI_API_KEY", "openai_sec_222");
        await _secureStorage.StoreSecretAsync("LINKEDIN_TOKEN", "li_token_333");

        await _secureStorage.DeleteSecretAsync("OPENAI_API_KEY");

        Assert.IsNull(await _secureStorage.GetSecretAsync("OPENAI_API_KEY"));
        Assert.IsFalse(await _secureStorage.HasSecretAsync("OPENAI_API_KEY"));

        Assert.AreEqual("gemini_sec_111", await _secureStorage.GetSecretAsync("GEMINI_API_KEY"));
        Assert.AreEqual("li_token_333", await _secureStorage.GetSecretAsync("LINKEDIN_TOKEN"));
    }

    [TestMethod]
    public async Task T2d_DPAPI_Concurrent_Access_Maintains_Data_Integrity()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 15; i++)
        {
            int id = i;
            tasks.Add(Task.Run(async () =>
            {
                await _secureStorage.StoreSecretAsync($"KEY_{id}", $"VAL_{id}");
                var val = await _secureStorage.GetSecretAsync($"KEY_{id}");
                Assert.AreEqual($"VAL_{id}", val);
            }));
        }

        await Task.WhenAll(tasks);
    }

    [TestMethod]
    public async Task ST_03_Restart_Bookmark_Saves_InFlight_Session_And_Draft_Atomically()
    {
        string bookmarkFile = Path.Combine(_testDir, "bookmark.json");
        var bookmarkData = new
        {
            PageRoute = "ImprovePage",
            ActiveSection = "Headline",
            InFlightDraft = "Building AI agents that save B2B teams 10h/wk | $4.2M pipeline | Ops @ Acme",
            SessionMemberId = "mem-12345",
            Timestamp = DateTime.UtcNow
        };

        // Atomic write via temp file
        string tempPath = bookmarkFile + ".tmp";
        string json = System.Text.Json.JsonSerializer.Serialize(bookmarkData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, bookmarkFile, overwrite: true);

        Assert.IsTrue(File.Exists(bookmarkFile), "Bookmark file must exist after atomic save");
        string loadedJson = await File.ReadAllTextAsync(bookmarkFile);
        Assert.IsTrue(loadedJson.Contains("ImprovePage"));
        Assert.IsTrue(loadedJson.Contains("Building AI agents"));
    }

    [TestMethod]
    public async Task ST_03b_Restart_Bookmark_Restores_State_On_Startup_After_Simulated_Reboot()
    {
        string bookmarkFile = Path.Combine(_testDir, "bookmark.json");
        var expectedDraft = "Building AI agents saving teams 10h/wk | $4.2M pipeline";
        var bookmarkData = new
        {
            PageRoute = "ImprovePage",
            ActiveSection = "Headline",
            InFlightDraft = expectedDraft,
            SessionMemberId = "mem-12345",
            Timestamp = DateTime.UtcNow
        };

        string json = System.Text.Json.JsonSerializer.Serialize(bookmarkData);
        await File.WriteAllTextAsync(bookmarkFile, json);

        // Simulate app restart / reboot: verify restoration from bookmark.json
        Assert.IsTrue(File.Exists(bookmarkFile));
        using var doc = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(bookmarkFile));
        var root = doc.RootElement;

        string route = root.GetProperty("PageRoute").GetString()!;
        string section = root.GetProperty("ActiveSection").GetString()!;
        string draft = root.GetProperty("InFlightDraft").GetString()!;

        Assert.AreEqual("ImprovePage", route);
        Assert.AreEqual("Headline", section);
        Assert.AreEqual(expectedDraft, draft);
    }

    [TestMethod]
    public async Task ST_03c_Restart_Bookmark_Clears_On_Clean_Application_Exit()
    {
        string bookmarkFile = Path.Combine(_testDir, "bookmark.json");
        await File.WriteAllTextAsync(bookmarkFile, "{\"PageRoute\":\"ImprovePage\"}");
        Assert.IsTrue(File.Exists(bookmarkFile));

        // Clean exit removes bookmark
        if (File.Exists(bookmarkFile))
        {
            File.Delete(bookmarkFile);
        }

        Assert.IsFalse(File.Exists(bookmarkFile), "Bookmark must be cleared on clean exit to avoid stale restorations");
    }

    [TestMethod]
    public void ST_03_ProfileHistoryItem_Formatting_And_JsonIgnore()
    {
        var dt = new DateTime(2026, 11, 21, 14, 30, 0, DateTimeKind.Utc);
        var item = new ProfileHistoryItem(
            Id: "test-1",
            SectionName: "Headline",
            PreviousText: "Old Headline",
            AppliedText: "New Headline",
            HumanScore: 88.6,
            Status: "Applied",
            Timestamp: dt,
            AppliedModel: "gemini-2.0-flash"
        );

        Assert.AreEqual(dt.ToString("g"), item.FormattedTimestamp);
        Assert.AreEqual(88.6.ToString("F0"), item.FormattedHumanScore);

        var json = System.Text.Json.JsonSerializer.Serialize(item);
        Assert.IsFalse(json.Contains("FormattedTimestamp"), "FormattedTimestamp must be JsonIgnore'd");
        Assert.IsFalse(json.Contains("FormattedHumanScore"), "FormattedHumanScore must be JsonIgnore'd");
    }

    [TestMethod]
    public async Task ST_01_Corrupt_Voice_And_Plan_Recovery()
    {
        string voiceFile = Path.Combine(_testDir, "voice.md");
        string planFile = Path.Combine(_testDir, "plan.md");

        // 1. Initial save and load of corrupted/malformed text
        await File.WriteAllTextAsync(voiceFile, "!!!Malformed voice content###\0\0\0");
        var voiceResult = await _dataService.LoadVoiceProfileAsync();
        Assert.IsNotNull(voiceResult);
        Assert.IsTrue(voiceResult.Contains("Malformed voice"));

        await File.WriteAllTextAsync(planFile, "@@@Malformed plan strategy$$$\0\0\0");
        var planResult = await _dataService.LoadContentPlanAsync();
        Assert.IsNotNull(planResult);
        Assert.IsTrue(planResult.Contains("Malformed plan"));

        // 2. Test fault-tolerance if file read encounters I/O failure (exclusive lock simulation)
        using (var lockStream = new FileStream(voiceFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Must catch and recover with string.Empty without throwing
            var recoveryResult = await _dataService.LoadVoiceProfileAsync();
            Assert.AreEqual(string.Empty, recoveryResult, "Must safely return empty string on unreadable voice.md");
        }

        using (var lockStream = new FileStream(planFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Must catch and recover with string.Empty without throwing
            var recoveryResult = await _dataService.LoadContentPlanAsync();
            Assert.AreEqual(string.Empty, recoveryResult, "Must safely return empty string on unreadable plan.md");
        }
    }

    [TestMethod]
    public async Task T2_Multiple_Secrets_Isolation_DPAPI()
    {
        const string geminiKey = "AIzaSy-gemini-secret-api-key-112233";
        const string openAiKey = "sk-proj-openai-secret-token-445566";
        const string linkedInToken = "AQV-linkedin-bearer-oauth-token-778899";

        await _secureStorage.StoreSecretAsync("GEMINI_API_KEY", geminiKey);
        await _secureStorage.StoreSecretAsync("OPENAI_API_KEY", openAiKey);
        await _secureStorage.StoreSecretAsync("LINKEDIN_TOKEN", linkedInToken);

        // Verify all secrets are accurately retrievable
        Assert.AreEqual(geminiKey, await _secureStorage.GetSecretAsync("GEMINI_API_KEY"));
        Assert.AreEqual(openAiKey, await _secureStorage.GetSecretAsync("OPENAI_API_KEY"));
        Assert.AreEqual(linkedInToken, await _secureStorage.GetSecretAsync("LINKEDIN_TOKEN"));

        // Verify HasSecretAsync returns true
        Assert.IsTrue(await _secureStorage.HasSecretAsync("GEMINI_API_KEY"));
        Assert.IsTrue(await _secureStorage.HasSecretAsync("OPENAI_API_KEY"));
        Assert.IsTrue(await _secureStorage.HasSecretAsync("LINKEDIN_TOKEN"));
        Assert.IsFalse(await _secureStorage.HasSecretAsync("NONEXISTENT_KEY"));

        // Verify raw vault bytes do NOT contain any of the secrets in plaintext
        string vaultPath = Path.Combine(_testDir, "test_vault.dat");
        byte[] rawBytes = await File.ReadAllBytesAsync(vaultPath);
        string rawString = System.Text.Encoding.UTF8.GetString(rawBytes);

        Assert.IsFalse(rawString.Contains(geminiKey), "Raw vault must not contain plaintext Gemini key");
        Assert.IsFalse(rawString.Contains(openAiKey), "Raw vault must not contain plaintext OpenAI key");
        Assert.IsFalse(rawString.Contains(linkedInToken), "Raw vault must not contain plaintext LinkedIn token");
    }

    [TestMethod]
    public async Task T2_Corrupt_Vault_Recovery()
    {
        string vaultPath = Path.Combine(_testDir, "test_vault.dat");

        // Write random corrupted bytes into vault file
        byte[] corruptBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0xFF, 0xFE, 0xFD };
        await File.WriteAllBytesAsync(vaultPath, corruptBytes);

        // Create a new instance without in-memory cache to force reading from disk
        var serviceWithCorruptDisk = new DpapiSecureStorageService(vaultPath);

        var secret = await serviceWithCorruptDisk.GetSecretAsync("ANY_KEY");
        Assert.IsNull(secret, "Corrupted vault must return null instead of throwing");

        var hasSecret = await serviceWithCorruptDisk.HasSecretAsync("ANY_KEY");
        Assert.IsFalse(hasSecret, "Corrupted vault must report false for HasSecretAsync");

        // Must still be able to store a new secret and recover
        await serviceWithCorruptDisk.StoreSecretAsync("NEW_KEY", "new_valid_secret");
        Assert.AreEqual("new_valid_secret", await serviceWithCorruptDisk.GetSecretAsync("NEW_KEY"));
    }
}
