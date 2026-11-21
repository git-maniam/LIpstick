using System.Text.Json;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Core.Services;
using LIpstick.Infrastructure.AI;
using LIpstick.Infrastructure.LinkedIn;
using LIpstick.Infrastructure.Storage;

namespace LIpstick.Tests;

[TestClass]
public class IntegrationScenariosTests
{
    private string _testDir = null!;
    private MockLinkedInClient _client = null!;
    private LocalDataService _dataService = null!;
    private DpapiSecureStorageService _secureStorage = null!;
    private HumanizerEngine _humanizer = null!;
    private RubricEvaluator _rubricEvaluator = null!;
    private MockAIProvider _geminiProvider = null!;
    private MockAIProvider _openAiProvider = null!;
    private LinkedInSyncWorker _syncWorker = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "LIpstick_E2E_Integ_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _client = new MockLinkedInClient();
        _dataService = new LocalDataService(_testDir);
        _secureStorage = new DpapiSecureStorageService(Path.Combine(_testDir, "integ_vault.dat"));
        _humanizer = new HumanizerEngine();
        _rubricEvaluator = new RubricEvaluator();
        _geminiProvider = new MockAIProvider("Google Gemini");
        _openAiProvider = new MockAIProvider("OpenAI");
        _syncWorker = new LinkedInSyncWorker(_client, _dataService, _humanizer, _secureStorage);
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
    public async Task RE_01_E2E_Full_Lifecycle_Connect_Scan_Generate_Humanize_Diff_Apply_Rollback()
    {
        // 1. Connect
        var session = await _client.GetSessionInfoAsync();
        Assert.AreEqual(LinkedInConnectionState.Connected, session.State);
        Assert.AreEqual("Alex Vance", session.FullName);

        // 2. Ingest
        var profile = await _client.IngestProfileAsync();
        Assert.IsNotNull(profile);
        string originalHeadline = profile.Headline;

        // 3. Scan
        var rubricResult = _rubricEvaluator.EvaluateProfile(profile);
        Assert.AreEqual(12, rubricResult.Categories.Count);
        Assert.IsTrue(rubricResult.TotalScore < 80, "Default profile has missing signals to optimize");
        Assert.IsTrue(rubricResult.FixFirstCategories.Count > 0, "FixFirst items must be identified");
        Assert.IsTrue(rubricResult.Categories.Any(c => c.CategoryId == "headline" && c.PointsAwarded < c.MaxPoints), "Headline should be flagged for improvement");

        // 4. Generate
        var req = new RecommendationRequest(
            SectionName: "headline",
            CurrentText: profile.Headline,
            VoiceProfile: "Direct, pragmatic, outcome-driven",
            RubricCriteria: new Dictionary<string, object> { { "TargetScore", 12 } },
            ModelName: "gemini-2.0-flash"
        );
        string candidate = await _geminiProvider.GenerateRecommendationAsync(req);
        Assert.IsFalse(string.IsNullOrWhiteSpace(candidate));

        // 5. Humanize
        var (cleanedText, report) = _humanizer.CleanText(candidate);
        var humanScore = _humanizer.Evaluate(cleanedText);
        Assert.AreEqual(HumanizerVerdict.Pass, humanScore.Verdict, "Candidate rewrite must PASS Gate T5");
        Assert.IsTrue(humanScore.OverallScore >= 70.0);

        // 6. Diff
        var diffs = DiffEngine.ComputeWordDiff(originalHeadline, cleanedText);
        Assert.IsTrue(diffs.Any(d => d.Type == DiffChunkType.Deleted), "Diff must identify deleted old text");
        Assert.IsTrue(diffs.Any(d => d.Type == DiffChunkType.Inserted), "Diff must identify inserted new text");
        Assert.AreEqual(profile.Headline, originalHeadline, "Original profile headline must remain untouched (read-only)");

        // 7. Apply
        var applyResult = await _syncWorker.UpdateHeadlineAsync(cleanedText);
        Assert.AreEqual(UpdateOutcome.Succeeded, applyResult.Outcome);
        Assert.IsNotNull(applyResult.SnapshotId);

        // 8. History
        var history = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(1, history.Items.Count);
        Assert.AreEqual(originalHeadline, history.Items[0].PreviousText);
        Assert.AreEqual(cleanedText, history.Items[0].AppliedText);

        // 9. Rollback
        // Simulate profile currently having the applied headline
        _client.CustomProfile = MockLinkedInClient.CreateDefaultFixtureProfile() with { Headline = cleanedText };
        var rollbackResult = await _syncWorker.UpdateHeadlineAsync(history.Items[0].PreviousText);
        Assert.AreEqual(UpdateOutcome.Succeeded, rollbackResult.Outcome);

        var historyAfterRollback = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(2, historyAfterRollback.Items.Count, "Rollback must record an inverse audit entry in history");
        Assert.AreEqual(originalHeadline, historyAfterRollback.Items[1].AppliedText);
    }

    [TestMethod]
    public async Task RE_01b_E2E_MultiProvider_ModelHub_Switching_And_Failover_Workflow()
    {
        // 1. Initial provider: Gemini
        var geminiModels = await _geminiProvider.ListAvailableModelsAsync();
        Assert.IsTrue(geminiModels.Any(m => m.Id == "gemini-2.0-flash"));

        // 2. Simulate Gemini quota exceeded
        _geminiProvider.SimulateQuotaExceeded = true;
        var req = new RecommendationRequest("headline", "Old Title", "Voice", new Dictionary<string, object>(), "gemini-2.0-flash");

        var ex = await Assert.ThrowsExactlyAsync<AIProviderException>(async () =>
        {
            await _geminiProvider.GenerateRecommendationAsync(req);
        });
        Assert.AreEqual("RESOURCE_EXHAUSTED", ex.ErrorCode);

        // 3. Failover / Switch to OpenAI
        var openAiModels = await _openAiProvider.ListAvailableModelsAsync();
        Assert.IsTrue(openAiModels.Any(m => m.Id == "gpt-4o"));

        var openAiReq = req with { ModelName = "gpt-4o" };
        string openAiResult = await _openAiProvider.GenerateRecommendationAsync(openAiReq);
        Assert.IsFalse(string.IsNullOrWhiteSpace(openAiResult));

        // 4. Persist selection to AppConfig
        var config = await _dataService.LoadConfigAsync();
        var updatedConfig = config with { SelectedProvider = "OpenAI", SelectedModel = "gpt-4o" };
        await _dataService.SaveConfigAsync(updatedConfig);

        var reloaded = await _dataService.LoadConfigAsync();
        Assert.AreEqual("OpenAI", reloaded.SelectedProvider);
        Assert.AreEqual("gpt-4o", reloaded.SelectedModel);
    }

    [TestMethod]
    public async Task RE_01c_E2E_MultiSection_Partial_Failure_And_Integrity_Preservation()
    {
        string passingHeadline = "Building AI agents that save B2B teams 10 hours a week | $4.2M pipeline | Ops @ Acme";
        string slopAbout = "We delve into solutions. We spearhead synergy. We foster tapestries.";

        var batch = new Dictionary<string, string>
        {
            { "Headline", passingHeadline },
            { "About", slopAbout }
        };

        var batchResult = await _syncWorker.ApplyAllAsync(batch);
        Assert.AreEqual(UpdateOutcome.Failed, batchResult.Outcome);
        Assert.IsTrue(batchResult.Message!.Contains("Partial update"), "Must report partial update and never false all-success");

        var history = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(1, history.Items.Count, "Only the passing section should be recorded");
        Assert.AreEqual("Headline", history.Items[0].SectionName);
    }

    [TestMethod]
    public async Task RE_02_Full_Contract_Verification_Suite_Across_All_Providers()
    {
        var providers = new IAIProvider[] { _geminiProvider, _openAiProvider };

        foreach (var p in providers)
        {
            var models = await p.ListAvailableModelsAsync();
            Assert.IsTrue(models.Count >= 3, $"{p.ProviderName} must expose >= 3 models");
            foreach (var m in models)
            {
                Assert.IsTrue(m.ContextWindowTokens > 0, $"{m.Id} must have positive context window");
                Assert.IsTrue(m.EstimatedLatencyMs > 0, $"{m.Id} must have positive latency estimate");
            }
        }

        // Verify LinkedIn client contract
        await _client.DisconnectAsync();
        var session = await _client.GetSessionInfoAsync();
        Assert.AreEqual(LinkedInConnectionState.Disconnected, session.State);

        bool refreshed = await _client.RefreshTokenAsync();
        Assert.IsTrue(refreshed);

        // Verify storage round-trip contract
        await _secureStorage.StoreSecretAsync("CONTRACT_TEST", "val_123");
        var secret = await _secureStorage.GetSecretAsync("CONTRACT_TEST");
        Assert.AreEqual("val_123", secret);

        await _dataService.SaveVoiceProfileAsync("# Voice Contract Test");
        var voice = await _dataService.LoadVoiceProfileAsync();
        Assert.AreEqual("# Voice Contract Test", voice);
    }

    [TestMethod]
    public async Task RE_03_Cancellation_Mid_Flight_Preserves_Data_Safety_And_State()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
        {
            await _client.IngestProfileAsync(cts.Token);
        });

        var req = new RecommendationRequest("headline", "Text", "Voice", new Dictionary<string, object>(), "gemini-2.0-flash");
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
        {
            await _geminiProvider.GenerateRecommendationAsync(req, cts.Token);
        });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
        {
            await _syncWorker.UpdateHeadlineAsync("Draft", cts.Token);
        });

        var history = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(0, history.Items.Count, "Cancelled operations must not write to history");
    }

    [TestMethod]
    public async Task RE_03b_Crash_Safety_And_Orphan_Temp_File_Recovery()
    {
        // Setup valid history file
        var item1 = new ProfileHistoryItem("h-safe", "Headline", "Old", "New", 88.0, "Applied", DateTime.UtcNow, "gpt-4o");
        await _dataService.AppendHistoryAsync(item1);

        // Simulate crash leaving uncommitted .tmp file
        string historyFile = Path.Combine(_testDir, "history.json");
        string orphanTmp = historyFile + ".tmp";
        await File.WriteAllTextAsync(orphanTmp, "{ corrupted half-written stream...");

        // Load must recover valid data and ignore or overwrite orphan .tmp
        var log = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(1, log.Items.Count);
        Assert.AreEqual("h-safe", log.Items[0].Id);

        // Append must succeed atomically and overwrite the orphan temp file
        var item2 = new ProfileHistoryItem("h-safe-2", "About", "OldA", "NewA", 85.0, "Applied", DateTime.UtcNow, "gpt-4o");
        await _dataService.AppendHistoryAsync(item2);

        var updatedLog = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(2, updatedLog.Items.Count);
    }

    [TestMethod]
    public async Task RE_06_Reboot_Bookmark_Lifecycle_Saves_InFlight_State_And_Restores_After_Simulated_Crash()
    {
        string inFlightDraft = "Building AI agents saving teams 10 hours a week | $4.2M pipeline | Ops @ Acme";

        // 1. User is on ImprovePage with an in-flight uncommitted draft
        var checkpoint = new SessionBookmark
        {
            ActivePageRoute = "ImprovePage",
            ActiveSection = "Headline",
            UnsavedDraftText = inFlightDraft,
            LinkedInSessionState = "Connected",
            ActiveModelId = "gemini-2.0-flash",
            SavedAtUtc = DateTime.UtcNow
        };

        // Save atomic checkpoint through ILocalDataService
        await _dataService.SaveRestartBookmarkAsync(checkpoint);
        string bookmarkPath = Path.Combine(_testDir, "bookmark.json");
        Assert.IsTrue(File.Exists(bookmarkPath), "Checkpoint file must be written to disk by service");

        // 2. Simulate machine reboot / app restart
        // Fresh service instance simulates new application startup
        var rebootDataService = new LocalDataService(_testDir);
        var restored = await rebootDataService.GetRestartBookmarkAsync();

        Assert.IsNotNull(restored, "Bookmark must be recovered on startup after simulated reboot");
        Assert.AreEqual("ImprovePage", restored.ActivePageRoute, "Route must restore to ImprovePage");
        Assert.AreEqual("Headline", restored.ActiveSection, "Section must restore to Headline");
        Assert.AreEqual(inFlightDraft, restored.UnsavedDraftText, "Draft text must be preserved after crash/reboot");
        Assert.AreEqual("gemini-2.0-flash", restored.ActiveModelId);

        // 3. User applies the restored draft
        var applyResult = await _syncWorker.UpdateHeadlineAsync(restored.UnsavedDraftText!);
        Assert.AreEqual(UpdateOutcome.Succeeded, applyResult.Outcome);

        // 4. On clean completion / exit, bookmark is cleared via service
        await rebootDataService.ClearRestartBookmarkAsync();
        Assert.IsFalse(File.Exists(bookmarkPath), "Bookmark must be deleted after clean completion");
        var finalBookmark = await rebootDataService.GetRestartBookmarkAsync();
        Assert.IsNull(finalBookmark, "Bookmark must be null after clean clear");
    }
}
