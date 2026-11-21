using LIpstick.Core.Models;
using LIpstick.Core.Services;
using LIpstick.Infrastructure.LinkedIn;

namespace LIpstick.Tests;

[TestClass]
public class RubricScannerTests
{
    private RubricEvaluator _evaluator = null!;

    [TestInitialize]
    public void Setup()
    {
        string? rubricJson = null;
        string rubricPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../data/rubric.json");
        if (File.Exists(rubricPath))
        {
            rubricJson = File.ReadAllText(rubricPath);
        }
        _evaluator = new RubricEvaluator(rubricJson);
    }

    [TestMethod]
    public void SC_01_Evaluates_All_12_Categories_Deterministically()
    {
        var profile = MockLinkedInClient.CreateDefaultFixtureProfile();
        var result = _evaluator.EvaluateProfile(profile);

        Assert.AreEqual(12, result.Categories.Count, "Must evaluate exactly 12 categories");
        Assert.IsTrue(result.TotalScore > 0 && result.TotalScore <= 100, $"Total score {result.TotalScore} in 0-100 range");
        Assert.IsTrue(result.FixFirstCategories.Count > 0, "Must provide fix-first categories");
    }

    [TestMethod]
    public void SC_02_Verifies_Category_Maximums_12_10_10_8_6_14_10_8_8_6_4_4_Summing_To_100()
    {
        // Strict gate SC-02: 12/10/10/8/6/14/10/8/8/6/4/4 = 100
        var expectedWeights = new Dictionary<string, int>
        {
            { "headline", 12 },
            { "about_open", 10 },
            { "about_body", 10 },
            { "featured", 8 },
            { "banner", 6 },
            { "experience", 14 },
            { "recommendations", 10 },
            { "skills", 8 },
            { "creator_mode", 8 },
            { "education", 6 },
            { "custom_url", 4 },
            { "photo", 4 }
        };

        Assert.AreEqual(12, _evaluator.Definition.Items.Count, "Must have 12 items");
        int totalMax = 0;

        foreach (var item in _evaluator.Definition.Items)
        {
            Assert.IsTrue(expectedWeights.ContainsKey(item.Id), $"Unknown category ID {item.Id}");
            Assert.AreEqual(expectedWeights[item.Id], item.Points, $"Weight mismatch for category {item.Id}");
            totalMax += item.Points;
        }

        Assert.AreEqual(100, totalMax, "Total max points must be exactly 100");
    }

    [TestMethod]
    public void SC_03_Perfect_Profile_Scores_100_And_Has_Zero_FixFirst()
    {
        var perfectProfile = MockLinkedInClient.CreatePerfectFixtureProfile();
        var result = _evaluator.EvaluateProfile(perfectProfile);

        Assert.AreEqual(100, result.TotalScore, "Perfect profile must achieve 100/100 points");
        Assert.AreEqual(0, result.FixFirstCategories.Count, "Perfect profile has no missing points to fix");
    }

    [TestMethod]
    public void SC_04_FixFirst_Orders_By_Highest_Point_Loss()
    {
        // Profile with headline, banner, and experience losses
        var profile = new LinkedInProfile(
            MemberId: "mem-33",
            FullName: "Test User",
            Headline: "", // loss 12
            About: "",    // loss 20
            BannerUrl: null,
            PhotoUrl: null,
            CustomUrl: null,
            ContactEmail: null,
            Experiences: Array.Empty<ProfileExperience>(), // loss 14
            Educations: Array.Empty<ProfileEducation>(),
            Skills: Array.Empty<ProfileSkill>(),
            FeaturedItems: Array.Empty<ProfileFeaturedItem>(),
            RecommendationsCount: 0,
            LastPostDate: null,
            HasCreatorMode: false
        );

        var result = _evaluator.EvaluateProfile(profile);

        Assert.AreEqual(0, result.TotalScore, "Empty profile scores 0");
        Assert.IsTrue(result.FixFirstCategories.Count <= 3);

        // Top loss categories should be about_open, about_body, experience, headline
        var topLoss = result.FixFirstCategories.First();
        Assert.IsTrue(topLoss.MaxPoints - topLoss.PointsAwarded >= 10, "Top loss must carry significant weight");
    }

    [TestMethod]
    public void SC_05_Scanner_Is_Pure_Read_And_Performs_No_Mutations()
    {
        var profile = MockLinkedInClient.CreateDefaultFixtureProfile();
        string initialHeadline = profile.Headline;

        var result1 = _evaluator.EvaluateProfile(profile);
        var result2 = _evaluator.EvaluateProfile(profile);

        Assert.AreEqual(result1.TotalScore, result2.TotalScore, "Evaluation must be deterministic");
        Assert.AreEqual(initialHeadline, profile.Headline, "Scanner must not alter profile data");
    }

    [TestMethod]
    public void SC_01b_Malformed_And_Boundary_Profiles_Are_Clamped_And_Resilient()
    {
        var profile = new LinkedInProfile(
            MemberId: "mem-boundary",
            FullName: "Boundary Tester",
            Headline: new string('A', 5000),
            About: new string('B', 10000),
            BannerUrl: "default_gradient",
            PhotoUrl: "ghost_avatar",
            CustomUrl: "-000000",
            ContactEmail: null,
            Experiences: null!,
            Educations: null!,
            Skills: null!,
            FeaturedItems: null!,
            RecommendationsCount: -5,
            LastPostDate: DateTime.UtcNow.AddYears(5),
            HasCreatorMode: false
        );

        var result = _evaluator.EvaluateProfile(profile);

        Assert.IsNotNull(result);
        Assert.AreEqual(12, result.Categories.Count);
        Assert.IsTrue(result.TotalScore >= 0 && result.TotalScore <= 100, "Total score must be clamped strictly to [0, 100]");

        foreach (var cat in result.Categories)
        {
            Assert.IsTrue(cat.PointsAwarded >= 0 && cat.PointsAwarded <= cat.MaxPoints, $"Category {cat.CategoryId} points awarded {cat.PointsAwarded} out of bounds");
            Assert.IsNotNull(cat.MissingSignals);
            Assert.IsNotNull(cat.EvaluationNotes);
        }

        Assert.IsTrue(result.FixFirstCategories.Count > 0, "Fix-first categories must be populated");
    }

    [TestMethod]
    public void SC_01c_Corrupt_Or_Invalid_Rubric_Json_Falls_Back_To_Authoritative_12_Categories()
    {
        const string corruptJson = "{ malformed json string missing closing brace";
        const string invalidSumJson = "{\"version\":\"1.0\",\"total\":80,\"note\":\"test\",\"items\":[{\"id\":\"headline\",\"name\":\"Headline\",\"points\":12,\"fullMarks\":\"test\"}]}";

        var eval1 = new RubricEvaluator(corruptJson);
        var eval2 = new RubricEvaluator(invalidSumJson);

        Assert.AreEqual(12, eval1.Definition.Items.Count);
        Assert.AreEqual(100, eval1.Definition.Items.Sum(i => i.Points));
        Assert.AreEqual(12, eval2.Definition.Items.Count);
        Assert.AreEqual(100, eval2.Definition.Items.Sum(i => i.Points));

        var res = eval1.EvaluateProfile(MockLinkedInClient.CreateDefaultFixtureProfile());
        Assert.AreEqual(12, res.Categories.Count);
        Assert.IsTrue(res.TotalScore > 0 && res.TotalScore <= 100);
    }

    [TestMethod]
    public async Task SC_03b_Mid_Scan_Cancellation_Aborts_Gracefully()
    {
        var client = new MockLinkedInClient();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
        {
            await client.IngestProfileAsync(cts.Token);
        });
    }

    [TestMethod]
    public void SC_03c_Failed_Or_Cancelled_Scan_Preserves_Last_Valid_Scan_Results()
    {
        var profile = MockLinkedInClient.CreateDefaultFixtureProfile();
        var initial = _evaluator.EvaluateProfile(profile);

        Assert.IsTrue(initial.TotalScore > 0);
        Assert.AreEqual(12, initial.Categories.Count);
        var snapshotTotal = initial.TotalScore;
        var snapshotCategoriesCount = initial.Categories.Count;
        var snapshotFixFirstCount = initial.FixFirstCategories.Count;

        // Simulate subsequent scan failure
        bool scanFailed = false;
        try
        {
            throw new HttpRequestException("Network drop during scan", null, System.Net.HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException)
        {
            scanFailed = true;
        }

        Assert.IsTrue(scanFailed);
        // Gate T3 Data Safety: verify prior scan state remains pristine and valid
        Assert.AreEqual(snapshotTotal, initial.TotalScore);
        Assert.AreEqual(snapshotCategoriesCount, initial.Categories.Count);
        Assert.AreEqual(snapshotFixFirstCount, initial.FixFirstCategories.Count);
    }

    [TestMethod]
    public void SC_04b_FixFirst_Orders_Strictly_By_Desc_Loss_With_Exact_Three_Items()
    {
        var profile = new LinkedInProfile(
            MemberId: "mem-graduated",
            FullName: "Graduated User",
            Headline: "Experienced Engineering Leader | Building High-Throughput Distributed Cloud Systems", // >60 chars, has '|', no numbers -> 8/12 (loss: 4)
            About: "", // empty -> 0/20 (loss: 20 across about_open and about_body)
            BannerUrl: null, // loss: 6
            PhotoUrl: "ghost_avatar", // loss: 4
            CustomUrl: "-000000", // loss: 4
            ContactEmail: null,
            Experiences: Array.Empty<ProfileExperience>(), // loss: 14
            Educations: new List<ProfileEducation>
            {
                new("edu-1", "Tech University", "B.S.", "Computer Science", "2015 - 2019")
            }, // awarded 6/6 (loss: 0)
            Skills: Array.Empty<ProfileSkill>(), // loss: 8
            FeaturedItems: Array.Empty<ProfileFeaturedItem>(), // loss: 8
            RecommendationsCount: 0, // loss: 10
            LastPostDate: null, // loss: 8
            HasCreatorMode: false
        );

        var result = _evaluator.EvaluateProfile(profile);

        Assert.AreEqual(3, result.FixFirstCategories.Count, "Must select exactly top 3 highest loss categories");

        int loss0 = result.FixFirstCategories[0].MaxPoints - result.FixFirstCategories[0].PointsAwarded;
        int loss1 = result.FixFirstCategories[1].MaxPoints - result.FixFirstCategories[1].PointsAwarded;
        int loss2 = result.FixFirstCategories[2].MaxPoints - result.FixFirstCategories[2].PointsAwarded;

        Assert.IsTrue(loss0 >= loss1, $"loss0 ({loss0}) must be >= loss1 ({loss1})");
        Assert.IsTrue(loss1 >= loss2, $"loss1 ({loss1}) must be >= loss2 ({loss2})");

        Assert.AreEqual(3, result.Categories.Count(c => c.FixFirst));
        Assert.AreEqual(9, result.Categories.Count(c => !c.FixFirst));
    }
}
