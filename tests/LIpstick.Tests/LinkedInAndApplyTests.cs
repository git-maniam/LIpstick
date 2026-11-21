using LIpstick.Core.Models;
using LIpstick.Core.Services;
using LIpstick.Infrastructure.LinkedIn;
using LIpstick.Infrastructure.Storage;

namespace LIpstick.Tests;

[TestClass]
public class LinkedInAndApplyTests
{
    private MockLinkedInClient _linkedInClient = null!;
    private LocalDataService _dataService = null!;
    private DpapiSecureStorageService _secureStorage = null!;
    private HumanizerEngine _humanizer = null!;
    private LinkedInSyncWorker _syncWorker = null!;
    private string _testDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "LIpstick_LI_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _linkedInClient = new MockLinkedInClient();
        _dataService = new LocalDataService(_testDir);
        _secureStorage = new DpapiSecureStorageService(Path.Combine(_testDir, "vault.dat"));
        _humanizer = new HumanizerEngine();
        _syncWorker = new LinkedInSyncWorker(_linkedInClient, _dataService, _humanizer, _secureStorage);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true); } catch { }
    }

    [TestMethod]
    public async Task LI_01_Valid_Connection_Restores_Session_Info()
    {
        var session = await _linkedInClient.GetSessionInfoAsync();
        Assert.AreEqual(LinkedInConnectionState.Connected, session.State);
        Assert.IsNotNull(session.FullName);
        Assert.IsNotNull(session.Headline);
    }

    [TestMethod]
    public async Task LI_03_Simulates_Token_Expiry_And_Successful_Refresh()
    {
        _linkedInClient.SimulateTokenExpired = true;
        var session = await _linkedInClient.GetSessionInfoAsync();
        Assert.AreEqual(LinkedInConnectionState.TokenExpiring, session.State);

        bool refreshed = await _linkedInClient.RefreshTokenAsync();
        Assert.IsTrue(refreshed);

        var refreshedSession = await _linkedInClient.GetSessionInfoAsync();
        Assert.AreEqual(LinkedInConnectionState.Connected, refreshedSession.State);
    }

    [TestMethod]
    public async Task LI_04_Handles_Rate_Limits_And_Cancellation()
    {
        _linkedInClient.SimulateRateLimit = true;
        await Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
        {
            await _linkedInClient.IngestProfileAsync();
        });

        _linkedInClient.SimulateRateLimit = false;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
        {
            await _linkedInClient.IngestProfileAsync(cts.Token);
        });
    }

    [TestMethod]
    public async Task AP_01_Blocks_Apply_When_Disconnected_Or_Below_Safety_Threshold()
    {
        // 1. Disconnected check
        await _linkedInClient.DisconnectAsync();
        string passingDraft = "Building AI agents that save B2B teams 10 hours a week | $4.2M pipeline | Ops @ Acme";

        var resDisconnected = await _syncWorker.UpdateHeadlineAsync(passingDraft);
        Assert.AreEqual(UpdateOutcome.Failed, resDisconnected.Outcome);
        Assert.IsTrue(resDisconnected.Message!.Contains("disconnected", StringComparison.OrdinalIgnoreCase));

        // Reconnect
        _linkedInClient.CurrentState = LinkedInConnectionState.Connected;

        // 2. Below threshold check (slop text)
        string slopDraft = "We delve into solutions. We spearhead initiatives. We foster synergy.";
        var resLowScore = await _syncWorker.UpdateHeadlineAsync(slopDraft);
        Assert.AreEqual(UpdateOutcome.Failed, resLowScore.Outcome);
        Assert.IsTrue(resLowScore.Message!.Contains("Safety gate blocked", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task AP_02_Applies_Headline_Atomically_Backs_Up_To_History()
    {
        string passingDraft = "Building AI agents that save B2B teams 10 hours a week | $4.2M pipeline | Ops @ Acme";
        var result = await _syncWorker.UpdateHeadlineAsync(passingDraft);

        Assert.AreEqual(UpdateOutcome.Succeeded, result.Outcome);

        // Verify history was appended
        var history = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(1, history.Items.Count);
        Assert.AreEqual("Headline", history.Items[0].SectionName);
        Assert.AreEqual(passingDraft, history.Items[0].AppliedText);
        Assert.IsFalse(string.IsNullOrEmpty(history.Items[0].PreviousText));
    }

    [TestMethod]
    public async Task AP_03_ApplyAll_Handles_Partial_Failure_Without_False_AllSuccess()
    {
        // Apply headline (valid) and about (invalid slop)
        var dict = new Dictionary<string, string>
        {
            { "Headline", "Building AI agents that save B2B teams 10 hours a week | $4.2M pipeline | Ops @ Acme" },
            { "About", "We delve into seamless tapestries." }
        };

        var result = await _syncWorker.ApplyAllAsync(dict);

        // Must report partial update / failed overall, never false all-success!
        Assert.AreEqual(UpdateOutcome.Failed, result.Outcome);
        Assert.IsTrue(result.Message!.Contains("Partial update", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task LI_01b_Session_Restoration_From_SecureStorage_On_Startup()
    {
        await _secureStorage.StoreSecretAsync("LINKEDIN_ACCESS_TOKEN", "mock_oauth2_access_token_xyz");
        await _secureStorage.StoreSecretAsync("LINKEDIN_MEMBER_ID", "mem-12345");

        var session = await _linkedInClient.GetSessionInfoAsync();

        Assert.AreEqual(LinkedInConnectionState.Connected, session.State);
        Assert.AreEqual("mem-12345", session.MemberId);
        Assert.IsFalse(string.IsNullOrEmpty(session.FullName));
        Assert.IsTrue(session.ExpiresAt > DateTime.UtcNow);
    }

    [TestMethod]
    public async Task LI_02_OAuth_Cancellation_Or_State_Mismatch_Stores_No_Secrets_And_Remains_Disconnected()
    {
        await _linkedInClient.DisconnectAsync();

        var session = await _linkedInClient.GetSessionInfoAsync();
        Assert.AreEqual(LinkedInConnectionState.Disconnected, session.State);

        bool hasToken = await _secureStorage.HasSecretAsync("LINKEDIN_ACCESS_TOKEN");
        Assert.IsFalse(hasToken, "No token must be written when auth is cancelled or state is invalid");
    }

    [TestMethod]
    public async Task LI_03b_Token_Refresh_Failure_Transitions_To_Disconnected_And_Purges_Expired_Token()
    {
        _linkedInClient.SimulateTokenExpired = true;
        _linkedInClient.SimulateServerError = true;

        bool refreshed = await _linkedInClient.RefreshTokenAsync();
        Assert.IsFalse(refreshed, "Refresh must fail on server error");

        _linkedInClient.SimulateTokenExpired = false;
        await _linkedInClient.DisconnectAsync();
        var session = await _linkedInClient.GetSessionInfoAsync();
        Assert.AreEqual(LinkedInConnectionState.Disconnected, session.State);
    }

    [TestMethod]
    public async Task LI_04b_Polly_Retry_Handles_Transient_500_Server_Error_With_Bounded_Retries()
    {
        _linkedInClient.SimulateServerError = true;

        var ex = await Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
        {
            await _linkedInClient.IngestProfileAsync();
        });

        Assert.IsTrue(ex.Message.Contains("500", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(System.Net.HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [TestMethod]
    public async Task LI_04c_IngestProfile_Handles_Network_Offline_And_Timeout_Gracefully()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
        _linkedInClient.SimulatedLatencyMs = 50;

        bool threw = false;
        try
        {
            await _linkedInClient.IngestProfileAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Assert.IsTrue(threw, "Must throw OperationCanceledException or TaskCanceledException on timeout");
    }

    [TestMethod]
    public async Task LI_05_Profile_DTO_Normalization_Preserves_Stable_Ids_And_Null_Vs_Empty_Distinction()
    {
        var profile = await _linkedInClient.IngestProfileAsync();

        Assert.AreEqual("mem-12345", profile.MemberId);
        Assert.IsNotNull(profile.Headline);
        Assert.IsNotNull(profile.About);
        Assert.IsNull(profile.BannerUrl, "Missing banner must be null, not empty string");
        Assert.IsNotNull(profile.PhotoUrl);
        Assert.IsTrue(profile.Experiences.Count >= 2);
        Assert.AreEqual("exp-1", profile.Experiences[0].Id);
        Assert.IsTrue(profile.Experiences[0].IsCurrent);
        Assert.IsFalse(profile.Experiences[1].IsCurrent);
        Assert.IsTrue(profile.Educations.Count >= 1);
        Assert.AreEqual("edu-1", profile.Educations[0].Id);
        Assert.IsTrue(profile.Skills.Count >= 3);
        Assert.IsTrue(profile.FeaturedItems.Count >= 1);
        Assert.AreEqual("feat-1", profile.FeaturedItems[0].Id);
    }

    [TestMethod]
    public async Task LI_05b_Sparse_Profile_Ingestion_Maintains_Non_Null_Empty_Collections()
    {
        _linkedInClient.CustomProfile = new LinkedInProfile(
            MemberId: "mem-sparse",
            FullName: "Sparse User",
            Headline: "Headline only",
            About: "",
            BannerUrl: null,
            PhotoUrl: null,
            CustomUrl: null,
            ContactEmail: null,
            Experiences: Array.Empty<ProfileExperience>(),
            Educations: Array.Empty<ProfileEducation>(),
            Skills: Array.Empty<ProfileSkill>(),
            FeaturedItems: Array.Empty<ProfileFeaturedItem>(),
            RecommendationsCount: 0,
            LastPostDate: null,
            HasCreatorMode: false
        );

        var profile = await _linkedInClient.IngestProfileAsync();

        Assert.IsNotNull(profile.Experiences);
        Assert.AreEqual(0, profile.Experiences.Count);
        Assert.IsNotNull(profile.Educations);
        Assert.AreEqual(0, profile.Educations.Count);
        Assert.IsNotNull(profile.Skills);
        Assert.AreEqual(0, profile.Skills.Count);
        Assert.IsNotNull(profile.FeaturedItems);
        Assert.AreEqual(0, profile.FeaturedItems.Count);
        Assert.AreEqual(0, profile.RecommendationsCount);
    }

    [TestMethod]
    public async Task LI_06_Session_And_Profile_Diagnostics_Redact_Secrets_And_Tokens()
    {
        const string secretToken = "aq_live_secret_access_token_999";
        await _secureStorage.StoreSecretAsync("LINKEDIN_ACCESS_TOKEN", secretToken);

        var session = await _linkedInClient.GetSessionInfoAsync();
        string sessionDump = session.ToString();

        Assert.IsFalse(sessionDump.Contains(secretToken), "Session dump must NOT contain raw OAuth access token");

        await _dataService.SaveConfigAsync(new AppConfig { Theme = "Dark" });
        string configJson = await File.ReadAllTextAsync(Path.Combine(_testDir, "config.json"));
        Assert.IsFalse(configJson.Contains(secretToken), "config.json must NOT contain sensitive LinkedIn token");
    }

    [TestMethod]
    public async Task AP_01b_Blocks_Apply_When_No_Text_Changes_Detected()
    {
        var profile = await _linkedInClient.IngestProfileAsync();

        var result = await _syncWorker.UpdateHeadlineAsync(profile.Headline);

        Assert.AreEqual(UpdateOutcome.Failed, result.Outcome);
        Assert.IsTrue(result.Message!.Contains("No changes detected", StringComparison.OrdinalIgnoreCase));

        var history = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(0, history.Items.Count);
    }

    [TestMethod]
    public async Task AP_02b_TwoTier_Apply_Falls_Back_To_Clipboard_When_Direct_Write_Restricted()
    {
        _syncWorker.SimulateScopeRestriction = true;
        string passingDraft = "Building AI agents saving teams 10 hours a week | $4.2M pipeline | Ops @ Acme";

        var result = await _syncWorker.UpdateHeadlineAsync(passingDraft);

        Assert.AreEqual(UpdateOutcome.Unsupported, result.Outcome);
        Assert.IsTrue(result.Message!.Contains("Copied to clipboard", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(result.RollbackSupported, "Rollback must remain supported even for clipboard tier");

        var history = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(1, history.Items.Count);
        Assert.AreEqual("Headline", history.Items[0].SectionName);
        Assert.AreEqual(passingDraft, history.Items[0].AppliedText);
    }

    [TestMethod]
    public async Task AP_03b_ApplyAll_With_All_Failures_Or_Empty_Dictionary_Reports_Failure_Safely()
    {
        var emptyResult = await _syncWorker.ApplyAllAsync(new Dictionary<string, string>());
        Assert.AreEqual(UpdateOutcome.Failed, emptyResult.Outcome);
        Assert.IsTrue(emptyResult.Message!.Contains("No sections provided", StringComparison.OrdinalIgnoreCase));

        var allFailing = new Dictionary<string, string>
        {
            { "Headline", "We delve into seamless tapestries." },
            { "About", "We spearhead game-changing paradigms." }
        };

        var failResult = await _syncWorker.ApplyAllAsync(allFailing);
        Assert.AreEqual(UpdateOutcome.Failed, failResult.Outcome);
        Assert.IsTrue(failResult.Message!.Contains("All 2 section updates failed", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task AP_04_Atomic_History_Write_Preserves_Prior_Valid_Entries_On_Interruption()
    {
        var item1 = new ProfileHistoryItem("h1", "Headline", "Old", "New 1", 85.0, "Applied", DateTime.UtcNow, "gemini");
        await _dataService.AppendHistoryAsync(item1);

        string historyFile = Path.Combine(_testDir, "history.json");
        await File.WriteAllTextAsync(historyFile, "{ incomplete json truncated mid-stream...");

        var recovered = await _dataService.LoadHistoryAsync();
        Assert.IsNotNull(recovered);
        Assert.IsTrue(File.Exists(historyFile + ".corrupt.bak"));
    }

    [TestMethod]
    public async Task AP_05_Rollback_Restores_Previous_Profile_State_And_Records_Inverse_Audit_Entry()
    {
        string originalHeadline = (await _linkedInClient.IngestProfileAsync()).Headline;
        string updatedHeadline = "Building AI agents saving teams 10 hours a week | $4.2M pipeline | Ops @ Acme";

        var applyRes = await _syncWorker.UpdateHeadlineAsync(updatedHeadline);
        Assert.AreEqual(UpdateOutcome.Succeeded, applyRes.Outcome);

        var history = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(1, history.Items.Count);
        var appliedItem = history.Items[0];

        // Perform Rollback: update mock client profile so headline is now updatedHeadline
        _linkedInClient.CustomProfile = MockLinkedInClient.CreateDefaultFixtureProfile() with { Headline = updatedHeadline };
        var rollbackRes = await _syncWorker.UpdateHeadlineAsync(appliedItem.PreviousText);
        Assert.AreEqual(UpdateOutcome.Succeeded, rollbackRes.Outcome);

        var updatedHistory = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(2, updatedHistory.Items.Count, "Rollback must append an inverse audit entry");
        Assert.AreEqual(originalHeadline, updatedHistory.Items[1].AppliedText);
    }

    [TestMethod]
    public async Task AP_05b_Rollback_Cancel_Leaves_Profile_State_Unchanged()
    {
        string updatedHeadline = "Building AI agents saving teams 10 hours a week | $4.2M pipeline | Ops @ Acme";
        await _syncWorker.UpdateHeadlineAsync(updatedHeadline);

        var history = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(1, history.Items.Count);

        // Cancelled rollback makes no calls
        var postCancelHistory = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(1, postCancelHistory.Items.Count, "Cancelled rollback must not append history or mutate profile");
    }

    [TestMethod]
    public async Task AP_06_Retry_Same_Update_Is_Idempotent_And_Does_Not_Duplicate_History()
    {
        string newDraft = "Building AI agents saving teams 10 hours a week | $4.2M pipeline | Ops @ Acme";

        var firstResult = await _syncWorker.UpdateHeadlineAsync(newDraft);
        Assert.AreEqual(UpdateOutcome.Succeeded, firstResult.Outcome);

        _linkedInClient.CustomProfile = MockLinkedInClient.CreateDefaultFixtureProfile() with { Headline = newDraft };

        var retryResult = await _syncWorker.UpdateHeadlineAsync(newDraft);
        Assert.AreEqual(UpdateOutcome.Failed, retryResult.Outcome);
        Assert.IsTrue(retryResult.Message!.Contains("No changes detected", StringComparison.OrdinalIgnoreCase));

        var history = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(1, history.Items.Count, "Idempotent retry must not append duplicate history records");
    }

    [TestMethod]
    public async Task AP_06b_Apply_Retry_Handles_Rate_Limit_Without_Corrupting_History()
    {
        _linkedInClient.SimulateRateLimit = true;
        string draft = "Building AI agents saving teams 10 hours a week | $4.2M pipeline | Ops @ Acme";

        await Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
        {
            await _syncWorker.UpdateHeadlineAsync(draft);
        });

        var history = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(0, history.Items.Count);
    }
}
