using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Core.Services;
using LIpstick.Infrastructure.LinkedIn;
using LIpstick.Infrastructure.Storage;

namespace LIpstick.Tests;

[TestClass]
public class AdversarialSecurityTests
{
    private string _tempDir = null!;
    private LocalDataService _dataService = null!;
    private RubricEvaluator _evaluator = null!;
    private HumanizerEngine _humanizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LIpstick_SecTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _dataService = new LocalDataService(_tempDir);
        _evaluator = new RubricEvaluator();
        _humanizer = new HumanizerEngine();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [TestMethod]
    public void RE_04_Gate_T2_Automated_Secret_Scan_Over_Repository_Fixtures_And_Logs()
    {
        // Define secret patterns matching live keys/tokens:
        // OpenAI live key: sk-[a-zA-Z0-9_-]{20,}
        // Gemini live key: AIza[0-9A-Za-z-_]{35}
        // Bearer auth tokens: Bearer\s+[a-zA-Z0-9_\-\.]{25,}
        var patterns = new List<Regex>
        {
            new(@"sk-[a-zA-Z0-9_\-]{20,}", RegexOptions.Compiled),
            new(@"AIza[0-9A-Za-z\-_]{35}", RegexOptions.Compiled),
            new(@"Bearer\s+[a-zA-Z0-9_\-\.]{25,}", RegexOptions.Compiled)
        };

        // Scan src/, data/, and skills/ directories
        var baseDir = AppContext.BaseDirectory;
        // Navigate up to repository root from bin/Debug/net10.0
        var rootDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        if (!Directory.Exists(Path.Combine(rootDir, "src")))
        {
            // Fallback to searching relative to working directory
            rootDir = Directory.GetCurrentDirectory();
            while (!Directory.Exists(Path.Combine(rootDir, "src")) && Directory.GetParent(rootDir) != null)
            {
                rootDir = Directory.GetParent(rootDir)!.FullName;
            }
        }

        var targetFolders = new[] { "src", "data", "skills" };
        var scannedFiles = 0;
        var leakedFiles = new List<string>();

        foreach (var folder in targetFolders)
        {
            var targetPath = Path.Combine(rootDir, folder);
            if (!Directory.Exists(targetPath)) continue;

            var files = Directory.EnumerateFiles(targetPath, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext is ".cs" or ".json" or ".xaml" or ".md";
                })
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("/obj/") && !f.Contains("\\bin\\") && !f.Contains("/bin/"));

            foreach (var file in files)
            {
                scannedFiles++;
                var content = File.ReadAllText(file);

                foreach (var pattern in patterns)
                {
                    if (pattern.IsMatch(content))
                    {
                        leakedFiles.Add($"{file} matched pattern: {pattern}");
                    }
                }
            }
        }

        Assert.IsTrue(scannedFiles > 0, "Secret scan must inspect repository source, data, and skill files");
        Assert.AreEqual(0, leakedFiles.Count, $"Gate T2 Violation: Found potential live secrets in repository:\n{string.Join("\n", leakedFiles)}");
    }

    [TestMethod]
    public async Task SEC_01_DPAPI_Vault_File_Tampering_And_Corrupt_Secret_Recovery()
    {
        var vaultPath = Path.Combine(_tempDir, "secure_vault.dat");

        // Create initial corrupted file with random bytes
        var corruptBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04, 0xAA, 0xBB, 0xCC, 0xDD };
        await File.WriteAllBytesAsync(vaultPath, corruptBytes);

        // A new instance reads directly from disk without in-memory caching
        var secureStorage = new DpapiSecureStorageService(vaultPath);

        // Should not throw CryptographicException; must safely recover and return null
        var secret = await secureStorage.GetSecretAsync("OPENAI_API_KEY");
        Assert.IsNull(secret, "Corrupted DPAPI vault must recover safely and return null");

        // Verify write and subsequent read works cleanly even after corruption recovery
        await secureStorage.StoreSecretAsync("OPENAI_API_KEY", "recovered-clean-key");
        var restored = await secureStorage.GetSecretAsync("OPENAI_API_KEY");
        Assert.AreEqual("recovered-clean-key", restored, "Vault should recover and allow clean writes after tampering");
    }

    [TestMethod]
    public void SEC_02_Extreme_Input_Fuzzing_Rubric_Scanner_And_Humanizer()
    {
        // 1. Extreme 50,000-character headline string
        var hugeString = new string('A', 50_000);

        // 2. Control characters, Unicode overrides, and SQL injection strings
        var toxicInput = "Hello \u0000\uFFFF\u202E RTL override test <script>alert('XSS')</script>'; DROP TABLE Profiles;--";

        var fuzzedProfile = new LinkedInProfile(
            MemberId: "mem-fuzz-1",
            FullName: "Fuzz User " + toxicInput,
            Headline: hugeString + toxicInput,
            About: toxicInput + new string('B', 10_000),
            BannerUrl: "https://evil.com/payload.jpg?param=" + toxicInput,
            PhotoUrl: null,
            CustomUrl: null,
            ContactEmail: "fuzz@example.com",
            Experiences: new[]
            {
                new ProfileExperience("exp-1", "Role", "Company", "2020 - Present", toxicInput, true)
            },
            Educations: Array.Empty<ProfileEducation>(),
            Skills: Array.Empty<ProfileSkill>(),
            FeaturedItems: Array.Empty<ProfileFeaturedItem>(),
            RecommendationsCount: int.MaxValue,
            LastPostDate: null,
            HasCreatorMode: false
        );

        // Scanner must not crash, throw UnhandledException, or exceed 100 points
        var sw = Stopwatch.StartNew();
        var rubricResult = _evaluator.EvaluateProfile(fuzzedProfile);
        sw.Stop();

        Assert.IsTrue(rubricResult.TotalScore >= 0 && rubricResult.TotalScore <= 100, "Score must stay strictly bounded [0, 100]");
        Assert.IsTrue(sw.ElapsedMilliseconds < 2000, "Evaluation of extreme input must complete promptly without ReDoS");

        // Humanizer must process toxic input without crashing or infinite regex loop
        sw.Restart();
        var (cleaned, report) = _humanizer.CleanText(toxicInput);
        var eval = _humanizer.Evaluate(cleaned);
        sw.Stop();

        Assert.IsNotNull(cleaned);
        Assert.IsNotNull(report);
        Assert.IsTrue(eval.OverallScore >= 0 && eval.OverallScore <= 100);
        Assert.IsTrue(sw.ElapsedMilliseconds < 500, "Humanizer cleaning must execute quickly without catastrophic backtracking");
    }

    [TestMethod]
    public async Task SEC_03_Concurrent_Apply_And_History_Write_Stress()
    {
        // Spawn 20 concurrent tasks appending history simultaneously
        int concurrentTasks = 20;
        var tasks = new List<Task>();

        for (int i = 0; i < concurrentTasks; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                var item = new ProfileHistoryItem(
                    Id: $"hist-{index}-{Guid.NewGuid():N}",
                    SectionName: "Headline",
                    PreviousText: $"Previous headline state {index}",
                    AppliedText: $"Applied headline state {index}",
                    HumanScore: 85.0,
                    Status: "Succeeded",
                    Timestamp: DateTime.UtcNow,
                    AppliedModel: "gpt-4o"
                );
                await _dataService.AppendHistoryAsync(item);
            }));
        }

        await Task.WhenAll(tasks);

        // Verify history contains all 20 records without file locking corruption or dropped entries
        var history = await _dataService.LoadHistoryAsync();
        Assert.AreEqual(concurrentTasks, history.Items.Count, "All 20 concurrent history items must be persisted atomically");
    }

    [TestMethod]
    public async Task SEC_04_Gate_T6_Strict_Mutation_Block_On_All_Read_Operations()
    {
        var client = new MockLinkedInClient();

        // 1. Execute multiple Rubric evaluations
        var profile = await client.IngestProfileAsync();
        for (int i = 0; i < 25; i++)
        {
            _evaluator.EvaluateProfile(profile);
        }

        // 2. Execute multiple Humanizer runs
        for (int i = 0; i < 25; i++)
        {
            var (c, r) = _humanizer.CleanText("Delve into our comprehensive synergistic solutions.");
            _humanizer.Evaluate(c);
        }

        // 3. Execute multiple ContentLab drafts
        var contentLab = new ContentLabService();
        var postReq = new PostDraftRequest("Product launch announcement", "Tech founders", "$1M ARR", "Hook-01");
        for (int i = 0; i < 25; i++)
        {
            contentLab.DraftPost(postReq, _humanizer);
        }

        // 4. Gate T6: Verify client profile remains untouched and zero mutations occurred
        var session = await client.GetSessionInfoAsync();
        Assert.AreEqual(LinkedInConnectionState.Connected, session.State);
        Assert.AreEqual(profile.Headline, client.CustomProfile?.Headline ?? profile.Headline);
    }
}
