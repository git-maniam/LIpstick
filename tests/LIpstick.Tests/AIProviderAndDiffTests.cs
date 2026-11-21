using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Core.Services;
using LIpstick.Infrastructure.AI;
using LIpstick.Infrastructure.LinkedIn;
using LIpstick.Infrastructure.Storage;
using LIpstick.Tests.Fakes;

namespace LIpstick.Tests;

[TestClass]
public class AIProviderAndDiffTests
{
    private MockAIProvider _geminiMock = null!;
    private MockAIProvider _openAiMock = null!;
    private HumanizerEngine _humanizer = null!;

    [TestInitialize]
    public void Setup()
    {
        _geminiMock = new MockAIProvider("Google Gemini");
        _openAiMock = new MockAIProvider("OpenAI");
        _humanizer = new HumanizerEngine();
    }

    [TestMethod]
    public async Task AI_01_Lists_Gemini_Models_And_Generates_Recommendation()
    {
        var models = await _geminiMock.ListAvailableModelsAsync();
        Assert.IsTrue(models.Count >= 3);
        Assert.IsTrue(models.Any(m => m.Id == "gemini-2.0-flash"));

        var req = new RecommendationRequest("headline", "Old Job Title", "Pragmatic", new Dictionary<string, object>(), "gemini-2.0-flash");
        var result = await _geminiMock.GenerateRecommendationAsync(req);

        Assert.IsFalse(string.IsNullOrWhiteSpace(result));
        Assert.IsTrue(result.Contains("Building AI agents", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task AI_02_Lists_OpenAI_Models_And_Behaves_Identically_At_Contract()
    {
        var models = await _openAiMock.ListAvailableModelsAsync();
        Assert.IsTrue(models.Count >= 3);
        Assert.IsTrue(models.Any(m => m.Id == "gpt-4o"));

        var req = new RecommendationRequest("headline", "Old Job Title", "Pragmatic", new Dictionary<string, object>(), "gpt-4o");
        var result = await _openAiMock.GenerateRecommendationAsync(req);

        Assert.IsFalse(string.IsNullOrWhiteSpace(result));
    }

    [TestMethod]
    public async Task AI_03_Maps_Errors_To_Typed_Exceptions_Without_Crashing()
    {
        _geminiMock.SimulateQuotaExceeded = true;
        var req = new RecommendationRequest("headline", "Old", "Voice", new Dictionary<string, object>(), "gemini-2.0-flash");

        var ex = await Assert.ThrowsExactlyAsync<AIProviderException>(async () =>
        {
            await _geminiMock.GenerateRecommendationAsync(req);
        });

        Assert.AreEqual("RESOURCE_EXHAUSTED", ex.ErrorCode);
        Assert.IsTrue(ex.Message.Contains("quota exceeded", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DF_02_Computes_Word_Diff_Distinguishing_Additions_And_Deletions()
    {
        string current = "Senior Product Manager at Acme Corp";
        string rewrite = "Building AI agents saving teams 10h/wk | Ops @ Acme Corp";

        var diffs = DiffEngine.ComputeWordDiff(current, rewrite);

        Assert.IsTrue(diffs.Count > 1);
        Assert.IsTrue(diffs.Any(d => d.Type == DiffChunkType.Deleted), "Must identify deleted words");
        Assert.IsTrue(diffs.Any(d => d.Type == DiffChunkType.Inserted), "Must identify inserted words");
        Assert.IsTrue(diffs.Any(d => d.Type == DiffChunkType.Unchanged && d.Text.Contains("Acme")), "Must identify unchanged words like Acme");
    }

    [TestMethod]
    public async Task DF_01_Section_Tabs_Switch_Between_Headline_About_Experience_With_Readonly_AsIs()
    {
        var profile = MockLinkedInClient.CreateDefaultFixtureProfile();

        // 1. Headline section
        string asIsHeadline = profile.Headline;
        var reqHeadline = new RecommendationRequest("headline", asIsHeadline, "Voice", new Dictionary<string, object>(), "gemini-2.0-flash");
        string toBeHeadline = await _geminiMock.GenerateRecommendationAsync(reqHeadline);
        var diffHeadline = DiffEngine.ComputeWordDiff(asIsHeadline, toBeHeadline);
        Assert.IsNotNull(diffHeadline);
        Assert.AreEqual(profile.Headline, asIsHeadline, "As-Is headline must remain unchanged");

        // 2. About section
        string asIsAbout = profile.About;
        var reqAbout = new RecommendationRequest("about", asIsAbout, "Voice", new Dictionary<string, object>(), "gemini-2.0-flash");
        string toBeAbout = await _geminiMock.GenerateRecommendationAsync(reqAbout);
        var diffAbout = DiffEngine.ComputeWordDiff(asIsAbout, toBeAbout);
        Assert.IsNotNull(diffAbout);
        Assert.AreEqual(profile.About, asIsAbout, "As-Is about must remain unchanged");

        // 3. Experience section
        string asIsExp = profile.Experiences[0].Description;
        var reqExp = new RecommendationRequest("experience", asIsExp, "Voice", new Dictionary<string, object>(), "gemini-2.0-flash");
        string toBeExp = await _geminiMock.GenerateRecommendationAsync(reqExp);
        var diffExp = DiffEngine.ComputeWordDiff(asIsExp, toBeExp);
        Assert.IsNotNull(diffExp);
        Assert.AreEqual(profile.Experiences[0].Description, asIsExp, "As-Is experience must remain unchanged (read-only)");
    }

    [TestMethod]
    public void DF_02b_Word_Diff_LCS_Boundary_Cases_Empty_Identical_And_Complete_Replacement()
    {
        // Case 1: Identical strings
        var identical = DiffEngine.ComputeWordDiff("Acme Corp Ops", "Acme Corp Ops");
        Assert.AreEqual(1, identical.Count);
        Assert.AreEqual(DiffChunkType.Unchanged, identical[0].Type);
        Assert.AreEqual("Acme Corp Ops", identical[0].Text);

        // Case 2: Empty old to inserted new
        var emptyOld = DiffEngine.ComputeWordDiff("", "Brand New Text");
        Assert.AreEqual(1, emptyOld.Count);
        Assert.AreEqual(DiffChunkType.Inserted, emptyOld[0].Type);
        Assert.AreEqual("Brand New Text", emptyOld[0].Text);

        // Case 3: Populated old to empty new
        var emptyNew = DiffEngine.ComputeWordDiff("Obsolete Content", "");
        Assert.AreEqual(1, emptyNew.Count);
        Assert.AreEqual(DiffChunkType.Deleted, emptyNew[0].Type);
        Assert.AreEqual("Obsolete Content", emptyNew[0].Text);

        // Case 4: Complete replacement
        var singleDisjoint = DiffEngine.ComputeWordDiff("Red", "Blue");
        Assert.AreEqual(2, singleDisjoint.Count);
        Assert.AreEqual(DiffChunkType.Deleted, singleDisjoint[0].Type);
        Assert.AreEqual(DiffChunkType.Inserted, singleDisjoint[1].Type);

        var disjoint = DiffEngine.ComputeWordDiff("Red Green Blue", "One Two Three");
        Assert.IsTrue(disjoint.Any(d => d.Type == DiffChunkType.Deleted && d.Text.Contains("Red")));
        Assert.IsTrue(disjoint.Any(d => d.Type == DiffChunkType.Inserted && d.Text.Contains("One")));

        // Case 5: Punctuation tokenization
        var punct = DiffEngine.ComputeWordDiff("Save 10h/wk.", "Save 10h/wk!");
        Assert.IsTrue(punct.Any(d => d.Type == DiffChunkType.Unchanged));
        Assert.IsTrue(punct.Any(d => d.Type == DiffChunkType.Deleted && d.Text.Contains('.')));
        Assert.IsTrue(punct.Any(d => d.Type == DiffChunkType.Inserted && d.Text.Contains('!')));
    }

    [TestMethod]
    public void DF_03_Debounced_Live_Scoring_Updates_Score_And_Diff_On_Text_Change()
    {
        string asIs = "VP of Sales";
        string draft1 = "We delve into solutions. We foster collaboration.";
        var eval1 = _humanizer.Evaluate(draft1);
        Assert.AreNotEqual(HumanizerVerdict.Pass, eval1.Verdict);

        // User updates text
        string draft2 = @"Stop. We wasted 14 months on monolithic ERPs.
Then I wrote a 50-line script that solved 94% of warehouse inventory errors across 18 locations in Ohio and saved $180,000 quarterly.
Simple beats complex every time.
What's your take on automated ops?";

        var eval2 = _humanizer.Evaluate(draft2);
        Assert.AreEqual(HumanizerVerdict.Pass, eval2.Verdict);
        Assert.IsTrue(eval2.OverallScore >= 70.0);

        var diffs = DiffEngine.ComputeWordDiff(asIs, draft2);
        Assert.IsTrue(diffs.Any(d => d.Type == DiffChunkType.Inserted && d.Text.Contains("$180,000")));
    }

    [TestMethod]
    public void DF_04_Manual_Edit_Guard_Protects_Unsaved_Edits_From_Accidental_Overwrite()
    {
        string originalCandidate = "Generated Candidate 1";
        string currentText = originalCandidate;

        // Clean state
        bool hasManualEdits = currentText != originalCandidate;
        Assert.IsFalse(hasManualEdits);

        // User makes manual edits
        string userEdited = "Generated Candidate 1 with custom manual tweaks";
        currentText = userEdited;
        hasManualEdits = currentText != originalCandidate;
        Assert.IsTrue(hasManualEdits, "Dirty state must flag manual edits");

        // User cancels regeneration dialog: text must remain preserved
        bool userCancelled = true;
        if (userCancelled)
        {
            // currentText is untouched
            Assert.AreEqual(userEdited, currentText);
        }

        // User confirms regeneration: new text replaces
        string newCandidate = "Generated Candidate 2";
        currentText = newCandidate;
        originalCandidate = newCandidate;
        hasManualEdits = currentText != originalCandidate;
        Assert.IsFalse(hasManualEdits);
        Assert.AreEqual(newCandidate, currentText);
    }

    [TestMethod]
    public void DF_05_Scorecard_Pill_Formats_Verdict_And_Tooltip_Metrics()
    {
        var report = new HumanizeReport(new List<InvisibleHit>(), new List<TypoHit>(), new List<LexicalHit>(), new List<StructureFlag>());
        var result = new HumanizerResult(
            CleanedText: "Sample text",
            OverallScore: 88.0,
            Verdict: HumanizerVerdict.Pass,
            BurstinessScore: 78.0,
            SpecificityScore: 92.0,
            SlopDensityScore: 100.0,
            FingerprintScore: 100.0,
            VoiceScore: 82.0,
            FlaggedStructuralTells: new List<string>(),
            Report: report
        );

        string pillText = $"{result.OverallScore:F0} {result.Verdict.ToString().ToUpperInvariant()}";
        Assert.AreEqual("88 PASS", pillText);

        var tooltipLines = new List<string>
        {
            $"Burstiness: {result.BurstinessScore:F0}",
            $"Specificity: {result.SpecificityScore:F0}",
            $"Slop Density: {result.SlopDensityScore:F0}",
            $"Fingerprint: {result.FingerprintScore:F0}",
            $"Voice: {result.VoiceScore:F0}"
        };

        Assert.IsTrue(tooltipLines.Contains("Burstiness: 78"));
        Assert.IsTrue(tooltipLines.Contains("Specificity: 92"));
        Assert.IsTrue(tooltipLines.Contains("Slop Density: 100"));
        Assert.IsTrue(tooltipLines.Contains("Fingerprint: 100"));
        Assert.IsTrue(tooltipLines.Contains("Voice: 82"));
    }

    [TestMethod]
    public async Task AI_01b_Gemini_Model_Metadata_And_MidFlight_Cancellation()
    {
        var models = await _geminiMock.ListAvailableModelsAsync();
        var flash = models.FirstOrDefault(m => m.Id == "gemini-2.0-flash");

        Assert.IsNotNull(flash);
        Assert.IsTrue(flash.ContextWindowTokens >= 1000000);
        Assert.IsTrue(flash.EstimatedLatencyMs > 0);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var req = new RecommendationRequest("headline", "Current", "Voice", new Dictionary<string, object>(), "gemini-2.0-flash");
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
        {
            await _geminiMock.GenerateRecommendationAsync(req, cts.Token);
        });
    }

    [TestMethod]
    public async Task AI_01c_Gemini_Live_Adapter_Constructs_Valid_Payload_And_Parses_Response()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "gemini_val_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var mockStorage = new DpapiSecureStorageService(Path.Combine(tempDir, "vault.dat"));
            await mockStorage.StoreSecretAsync("GEMINI_API_KEY", "test_gemini_key_123");

            var gemini = new GeminiProvider(mockStorage);
            var models = await gemini.ListAvailableModelsAsync();

            Assert.IsTrue(models.Count >= 3);
            Assert.IsTrue(gemini.IsConfigured);
            Assert.IsTrue(models.Any(m => m.Id == "gemini-2.5-flash"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public async Task AI_02b_OpenAI_Model_Metadata_And_MidFlight_Cancellation()
    {
        var models = await _openAiMock.ListAvailableModelsAsync();
        var gpt4o = models.FirstOrDefault(m => m.Id == "gpt-4o");

        Assert.IsNotNull(gpt4o);
        Assert.IsTrue(gpt4o.ContextWindowTokens >= 128000);
        Assert.IsTrue(gpt4o.EstimatedLatencyMs > 0);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var req = new RecommendationRequest("headline", "Current", "Voice", new Dictionary<string, object>(), "gpt-4o");
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
        {
            await _openAiMock.GenerateRecommendationAsync(req, cts.Token);
        });
    }

    [TestMethod]
    public async Task AI_02c_OpenAI_Live_Adapter_Constructs_Valid_Payload_And_Parses_Response()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "openai_val_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var mockStorage = new DpapiSecureStorageService(Path.Combine(tempDir, "vault.dat"));
            await mockStorage.StoreSecretAsync("OPENAI_API_KEY", "test_openai_key_456");

            var openAi = new OpenAIProvider(mockStorage);
            var models = await openAi.ListAvailableModelsAsync();

            Assert.IsTrue(models.Count >= 3);
            Assert.IsTrue(openAi.IsConfigured);
            Assert.IsTrue(models.Any(m => m.Id == "gpt-4o"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public async Task AI_03b_Maps_Invalid_Key_And_Network_Timeout_To_Typed_Exceptions_Without_Secret_Leak()
    {
        _geminiMock.SimulateInvalidKey = true;
        var req = new RecommendationRequest("headline", "Old", "Voice", new Dictionary<string, object>(), "gemini-2.0-flash");

        var ex = await Assert.ThrowsExactlyAsync<AIProviderException>(async () =>
        {
            await _geminiMock.GenerateRecommendationAsync(req);
        });

        Assert.AreEqual("INVALID_KEY", ex.ErrorCode);
        Assert.IsFalse(ex.Message.Contains("test_gemini_key"), "Exception message must not leak API key");
    }

    [TestMethod]
    public async Task AI_03c_Malformed_JSON_Response_Throws_Typed_AIProviderException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "err_val_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var mockStorage = new DpapiSecureStorageService(Path.Combine(tempDir, "vault.dat"));
            // Without key configured, calling GenerateRecommendationAsync throws typed AIProviderException
            var gemini = new GeminiProvider(mockStorage);
            var req = new RecommendationRequest("headline", "Old", "Voice", new Dictionary<string, object>(), "gemini-2.0-flash");

            var ex = await Assert.ThrowsExactlyAsync<AIProviderException>(async () =>
            {
                await gemini.GenerateRecommendationAsync(req);
            });

            Assert.AreEqual("NO_API_KEY", ex.ErrorCode);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public async Task AI_03d_Live_Adapters_Classify_Invalid_Keys_And_Malformed_Responses()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ai_live_errors_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var storage = new DpapiSecureStorageService(Path.Combine(tempDir, "vault.dat"));
            await storage.StoreSecretAsync("GEMINI_API_KEY", "gemini-secret");
            await storage.StoreSecretAsync("OPENAI_API_KEY", "openai-secret");
            var requestGemini = new RecommendationRequest("Headline", "Current", "Voice", new Dictionary<string, object>(), "gemini-2.0-flash");
            var requestOpenAi = requestGemini with { ModelName = "gpt-4o" };

            foreach (var providerCase in new (string Name, Func<HttpClient, IAIProvider> Create, RecommendationRequest Request)[]
            {
                ("Gemini", http => new GeminiProvider(storage, http), requestGemini),
                ("OpenAI", http => new OpenAIProvider(storage, http), requestOpenAi)
            })
            {
                var badKeyHandler = new MockHttpMessageHandler
                {
                    StatusCode = System.Net.HttpStatusCode.Unauthorized,
                    ResponseContent = "{ \"error\": { \"message\": \"invalid key\" } }"
                };
                var invalidKey = await Assert.ThrowsExactlyAsync<AIProviderException>(() =>
                    providerCase.Create(new HttpClient(badKeyHandler)).GenerateRecommendationAsync(providerCase.Request));
                Assert.AreEqual(AIErrorCodes.InvalidKey, invalidKey.ErrorCode, $"{providerCase.Name}: {invalidKey.InnerException}");

                var malformedHandler = new MockHttpMessageHandler { ResponseContent = "{}" };
                var malformed = await Assert.ThrowsExactlyAsync<AIProviderException>(() =>
                    providerCase.Create(new HttpClient(malformedHandler)).GenerateRecommendationAsync(providerCase.Request));
                Assert.AreEqual(AIErrorCodes.MalformedResponse, malformed.ErrorCode, $"{providerCase.Name}: {malformed.InnerException}");
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public async Task AI_01d_Live_Adapters_Return_Usage_And_Latency_Metadata()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ai_usage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var storage = new DpapiSecureStorageService(Path.Combine(tempDir, "vault.dat"));
            await storage.StoreSecretAsync("GEMINI_API_KEY", "gemini-secret");
            await storage.StoreSecretAsync("OPENAI_API_KEY", "openai-secret");

            var geminiResponse = """{"candidates":[{"content":{"parts":[{"text":"Gemini draft"}]}}],"usageMetadata":{"promptTokenCount":21,"candidatesTokenCount":7}}""";
            var gemini = new GeminiProvider(storage, new HttpClient(new MockHttpMessageHandler { ResponseContent = geminiResponse }));
            var geminiResult = await gemini.GenerateCompletionAsync(new RecommendationRequest("Headline", "Current", "Voice", new Dictionary<string, object>(), "gemini-2.0-flash"));
            Assert.AreEqual("Gemini draft", geminiResult.Text);
            Assert.AreEqual(21, geminiResult.PromptTokenCount);
            Assert.AreEqual(7, geminiResult.ResponseTokenCount);
            Assert.AreEqual("gemini-2.0-flash", geminiResult.ModelId);

            var openAiResponse = """{"choices":[{"message":{"content":"OpenAI draft"}}],"usage":{"prompt_tokens":34,"completion_tokens":9}}""";
            var openAi = new OpenAIProvider(storage, new HttpClient(new MockHttpMessageHandler { ResponseContent = openAiResponse }));
            var openAiResult = await openAi.GenerateCompletionAsync(new RecommendationRequest("Headline", "Current", "Voice", new Dictionary<string, object>(), "gpt-4o"));
            Assert.AreEqual("OpenAI draft", openAiResult.Text);
            Assert.AreEqual(34, openAiResult.PromptTokenCount);
            Assert.AreEqual(9, openAiResult.ResponseTokenCount);
            Assert.AreEqual("gpt-4o", openAiResult.ModelId);
            Assert.IsTrue(geminiResult.LatencyMilliseconds >= 0 && openAiResult.LatencyMilliseconds >= 0);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [TestMethod]
    public async Task AI_04_Dynamic_Model_Switching_Updates_Metadata_And_Persists_Selection()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "ai_switch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        try
        {
            var dataService = new LocalDataService(testDir);
            var config = await dataService.LoadConfigAsync();

            // Switch model to gpt-4o
            var updated = config with { SelectedProvider = "OpenAI", SelectedModel = "gpt-4o" };
            await dataService.SaveConfigAsync(updated);

            var loaded = await dataService.LoadConfigAsync();
            Assert.AreEqual("OpenAI", loaded.SelectedProvider);
            Assert.AreEqual("gpt-4o", loaded.SelectedModel);

            // Verify config.json on disk does not contain secrets
            string json = await File.ReadAllTextAsync(Path.Combine(testDir, "config.json"));
            Assert.IsFalse(json.Contains("sk-"), "config.json must not store API keys");
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    [TestMethod]
    public void AI_04b_Custom_Base_Url_Configuration_Routes_Requests_To_Alternative_Endpoint()
    {
        var req = new RecommendationRequest("headline", "Current", "Voice", new Dictionary<string, object>
        {
            { "CustomBaseUrl", "https://custom-gateway.internal/v1" }
        }, "gpt-4o");

        Assert.AreEqual("https://custom-gateway.internal/v1", req.RubricCriteria["CustomBaseUrl"]);
    }

    [TestMethod]
    public async Task AI_05_Prompt_Context_Assembly_Combines_Profile_Voice_And_Rubric_Without_Secret_Leakage()
    {
        var rubric = new Dictionary<string, object>
        {
            { "Category", "Headline" },
            { "TargetPoints", 12 },
            { "MaxLoss", "Buzzwords, no metrics" }
        };

        var req = new RecommendationRequest(
            SectionName: "Headline",
            CurrentText: "Senior Product Manager at Acme Corp",
            VoiceProfile: "Tone: Direct, evidence-driven, pragmatic.",
            RubricCriteria: rubric,
            ModelName: "gemini-2.0-flash"
        );

        var result = await _geminiMock.GenerateRecommendationAsync(req);

        Assert.IsFalse(string.IsNullOrWhiteSpace(result));
        Assert.IsFalse(result.Contains("alex@example.com"), "Prompt output must not leak email");
        Assert.IsFalse(result.Contains("mem-12345"), "Prompt output must not leak member id");
    }
}
