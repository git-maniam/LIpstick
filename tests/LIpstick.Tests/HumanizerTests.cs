using LIpstick.Core.Models;
using LIpstick.Core.Services;

namespace LIpstick.Tests;

[TestClass]
public class HumanizerTests
{
    private HumanizerEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        string? slopJson = null;
        string slopPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../data/slop.json");
        if (File.Exists(slopPath))
        {
            slopJson = File.ReadAllText(slopPath);
        }
        _engine = new HumanizerEngine(slopJson);
    }

    [TestMethod]
    public void HU_01_Removes_All_Configured_Invisible_Characters()
    {
        // Text containing zero-width space, joiners, soft hyphen, BOM, LTR marks
        string input = "Hello\u200B world\u200C of\u200D modern\u2060 software\uFEFF testing\u00AD with\u200E unicode.";
        var (cleaned, report) = _engine.CleanText(input);

        Assert.IsFalse(cleaned.Contains('\u200B'), "Must not contain U+200B");
        Assert.IsFalse(cleaned.Contains('\u200C'), "Must not contain U+200C");
        Assert.IsFalse(cleaned.Contains('\u200D'), "Must not contain U+200D");
        Assert.IsFalse(cleaned.Contains('\u2060'), "Must not contain U+2060");
        Assert.IsFalse(cleaned.Contains('\uFEFF'), "Must not contain U+FEFF");
        Assert.IsFalse(cleaned.Contains('\u00AD'), "Must not contain U+00AD");
        Assert.IsTrue(report.Invisible.Count > 0, "Must report invisible hits");
        Assert.AreEqual("Hello world of modern software testing with unicode.", cleaned);
    }

    [TestMethod]
    public void HU_02_Normalizes_Typography_EmDash_CurlyQuotes_Ellipsis()
    {
        // Em dash -> comma, curly quotes -> straight, ellipsis -> three periods
        string input = "We built systems — scalable and fast — with “state-of-the-art” design…";
        var (cleaned, report) = _engine.CleanText(input);

        Assert.IsFalse(cleaned.Contains('—'), "Em dash must be replaced");
        Assert.IsFalse(cleaned.Contains('“') || cleaned.Contains('”'), "Curly double quotes must be replaced");
        Assert.IsFalse(cleaned.Contains('…'), "Ellipsis character must be replaced");

        Assert.IsTrue(cleaned.Contains(", "), "Em dash should convert to comma");
        Assert.IsTrue(cleaned.Contains("\"state-of-the-art\""), "Quotes should be straight");
        Assert.IsTrue(cleaned.EndsWith("..."), "Ellipsis should be 3 periods");
        Assert.IsTrue(report.Typographic.Count > 0);
    }

    [TestMethod]
    public void HU_03_Scrubs_Stock_Slop_Vocabulary()
    {
        // delve, testament to, in the realm of, spearhead, seamless
        string input = "Let us delve into this solution. It is a testament to our ability to spearhead seamless workflows in the realm of operations.";
        var (cleaned, report) = _engine.CleanText(input);

        Assert.IsFalse(cleaned.Contains("delve", StringComparison.OrdinalIgnoreCase), "Should scrub 'delve'");
        Assert.IsFalse(cleaned.Contains("testament to", StringComparison.OrdinalIgnoreCase), "Should scrub 'testament to'");
        Assert.IsFalse(cleaned.Contains("spearhead", StringComparison.OrdinalIgnoreCase), "Should scrub 'spearhead'");
        Assert.IsFalse(cleaned.Contains("seamless", StringComparison.OrdinalIgnoreCase), "Should scrub 'seamless'");
        Assert.IsFalse(cleaned.Contains("in the realm of", StringComparison.OrdinalIgnoreCase), "Should scrub 'in the realm of'");

        Assert.IsTrue(report.Lexical.Count >= 3, "Report must document removed slop terms");
    }

    [TestMethod]
    public void HU_04_Evaluates_Five_Checks_And_Enforces_Thresholds()
    {
        // T5: PASS is overall >= 70 AND every metric >= 55
        string naturalText = @"Stop. We wasted 14 months on monolithic ERPs.
Then I wrote a 50-line script that solved 94% of warehouse inventory errors across 18 locations in Ohio and saved $180,000 quarterly.
Simple beats complex every time.
What's your take on automated ops?";

        var result = _engine.Evaluate(naturalText);

        Assert.IsTrue(result.BurstinessScore >= 55.0, $"Burstiness was {result.BurstinessScore}");
        Assert.IsTrue(result.SpecificityScore >= 55.0, $"Specificity was {result.SpecificityScore}");
        Assert.IsTrue(result.FingerprintScore >= 70.0, $"Fingerprint was {result.FingerprintScore}");
        Assert.IsTrue(result.OverallScore >= 70.0, $"Overall score was {result.OverallScore}");
        Assert.AreEqual(HumanizerVerdict.Pass, result.Verdict, "Must achieve PASS verdict");
    }

    [TestMethod]
    public void HU_05_Cleaning_Is_Idempotent_And_Safe()
    {
        string input = "We spearhead seamless solutions — delve into http://example.com/test?a=1&b=2 for details…";
        var (cleanOnce, report1) = _engine.CleanText(input);
        var (cleanTwice, report2) = _engine.CleanText(cleanOnce);

        Assert.AreEqual(cleanOnce, cleanTwice, "CleanText must be idempotent");
        Assert.IsTrue(cleanOnce.Contains("http://example.com/test?a=1&b=2"), "URLs must be protected and preserved");
        Assert.AreEqual(0, report2.TotalArtifactsRemoved, "Second pass removes 0 artifacts");
    }

    [TestMethod]
    public void HU_06_Fails_When_Any_Metric_Below_55()
    {
        // AI heavy text with no numbers, even sentences, slop terms
        string slopText = "We delve into solutions. We foster collaboration. We spearhead initiatives. We create tapestries.";
        var result = _engine.Evaluate(slopText);

        Assert.AreNotEqual(HumanizerVerdict.Pass, result.Verdict, "Low quality text must not PASS");
        Assert.IsTrue(result.Verdict == HumanizerVerdict.Review || result.Verdict == HumanizerVerdict.Flagged);
    }

    [TestMethod]
    public void HU_01b_Strips_Extended_Invisible_Characters_And_General_Category_Cf()
    {
        string input = "Alpha\u180EBeta\u061CGamma\u200FDeltaEpsilon";
        var (cleaned, report) = _engine.CleanText(input);

        Assert.AreEqual("AlphaBetaGammaDeltaEpsilon", cleaned);
        Assert.IsTrue(report.Invisible.Count >= 3, "Report must document removed invisible characters");
        Assert.IsFalse(cleaned.Any(c => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format));
    }

    [TestMethod]
    public void HU_02b_Normalizes_EnDash_NonBreakingSpaces_PunctuationSplices_And_SingleQuotes()
    {
        string input = "Growth was –12% in tech – media with ‘tailored’ tools, . High-speed\u00A0sync with thin\u2009space,\nNext line.";
        var (cleaned, report) = _engine.CleanText(input);

        Assert.IsTrue(cleaned.Contains("-12%"), "En dash before number must convert to hyphen");
        Assert.IsTrue(cleaned.Contains("tech, media"), "En dash between words must convert to comma");
        Assert.IsTrue(cleaned.Contains("'tailored'"), "Single curly quotes must convert to straight apostrophes");
        Assert.IsFalse(cleaned.Contains(", ."), "Punctuation splice ', .' must be cleaned to single period '.'");
        Assert.IsTrue(cleaned.Contains("High-speed sync"), "NBSP must convert to regular space");
        Assert.IsTrue(cleaned.Contains("thin space\n"), "Trailing comma before newline must be removed");
        Assert.IsTrue(report.Typographic.Count >= 3);
        Assert.IsTrue(report.Typographic.Sum(t => t.Count) >= 4);
    }

    [TestMethod]
    public void HU_03b_Slop_Scrubbing_Preserves_Casing_And_Flags_Structural_Tells()
    {
        string input = "Delve into our work. We SPEARHEAD solutions that are scalable, reliable, and secure. It is not only powerful but also fast.";
        var (cleaned, report) = _engine.CleanText(input);

        Assert.IsTrue(cleaned.StartsWith("Look at", StringComparison.Ordinal), "TitleCase 'Delve into' must replace with TitleCase 'Look at'");
        Assert.IsTrue(cleaned.Contains("LEAD solutions", StringComparison.Ordinal), "UPPERCASE 'SPEARHEAD' must replace with UPPERCASE 'LEAD'");
        Assert.IsTrue(report.Structures.Any(s => s.Name.Contains("three", StringComparison.OrdinalIgnoreCase)), "Must flag rule-of-three adjective structure");
        Assert.IsTrue(report.Structures.Any(s => s.Name.Contains("not only", StringComparison.OrdinalIgnoreCase)), "Must flag not-only-but structure");
    }

    [TestMethod]
    public void HU_04b_Exact_Threshold_Boundary_Enforcement_T5_Gate()
    {
        // Case A: High quality text passing all gates
        string naturalText = @"Stop. We wasted 14 months on monolithic ERPs.
Then I wrote a 50-line script that solved 94% of warehouse inventory errors across 18 locations in Ohio and saved $180,000 quarterly.
Simple beats complex every time.
What's your take on automated ops?";
        var evalPass = _engine.Evaluate(naturalText);
        Assert.IsTrue(evalPass.OverallScore >= 70.0);
        Assert.IsTrue(evalPass.BurstinessScore >= 55.0 && evalPass.SpecificityScore >= 55.0 && evalPass.SlopDensityScore >= 55.0 && evalPass.FingerprintScore >= 55.0 && evalPass.VoiceScore >= 55.0);
        Assert.AreEqual(HumanizerVerdict.Pass, evalPass.Verdict);

        // Case B: Quality Gate T5: Low-scoring text failing threshold
        string slopText = "We delve into solutions. We foster collaboration. We spearhead initiatives. We create tapestries.";
        var evalWeak = _engine.Evaluate(slopText);
        Assert.AreNotEqual(HumanizerVerdict.Pass, evalWeak.Verdict);
        Assert.IsTrue(evalWeak.Verdict == HumanizerVerdict.Review || evalWeak.Verdict == HumanizerVerdict.Flagged);

        // Case C: Empty/Whitespace text
        var evalEmpty = _engine.Evaluate("   \t\n   ");
        Assert.AreEqual(HumanizerVerdict.Flagged, evalEmpty.Verdict);
    }

    [TestMethod]
    public void HU_05b_Safeguards_Multiple_Urls_Emails_And_Markdown_Formatting()
    {
        string input = "Delve into https://api.acme.com/v2/items?filter=all&sort=asc#results and email contact@acme.corp for spearhead details. Also visit www.acme.org/portal.";
        var (cleaned, report) = _engine.CleanText(input);

        Assert.IsTrue(cleaned.Contains("https://api.acme.com/v2/items?filter=all&sort=asc#results"), "Full URL with params and anchor must be byte-for-byte preserved");
        Assert.IsTrue(cleaned.Contains("contact@acme.corp"), "Email address must be preserved");
        Assert.IsTrue(cleaned.Contains("www.acme.org/portal"), "www URL must be preserved");
        Assert.IsFalse(cleaned.Contains("delve", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cleaned.Contains("spearhead", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task HU_06b_Bounded_Rewrite_Loop_Terminates_At_Max_Attempts()
    {
        string initialText = "We delve into seamless synergy.";
        int callCount = 0;
        Func<string, CancellationToken, Task<string>> rewriteFunc = (prompt, ct) =>
        {
            callCount++;
            return Task.FromResult($"Attempt {callCount}: We delve into foster collaboration and spearhead seamless workflows.");
        };

        var result = await _engine.EnsurePassingAsync(initialText, rewriteFunc, maxAttempts: 3);
        Assert.AreEqual(3, callCount, "Loop must execute exactly maxAttempts times and terminate");
        Assert.AreNotEqual(HumanizerVerdict.Pass, result.Verdict, "Must not claim Pass when rewrites fail");
    }

    [TestMethod]
    public async Task HU_06c_Bounded_Rewrite_Loop_Exits_Early_On_Passing_Candidate()
    {
        string initialText = "delve into synergy";
        int callCount = 0;
        Func<string, CancellationToken, Task<string>> rewriteFunc = (prompt, ct) =>
        {
            callCount++;
            if (callCount == 1) return Task.FromResult("We spearhead seamless solutions.");
            return Task.FromResult("Stop. I cut 14 manual ERP tasks down to 2 automated scripts in Ohio, saving $120,000 quarterly. What ops grind do you hate?");
        };

        var result = await _engine.EnsurePassingAsync(initialText, rewriteFunc, maxAttempts: 5);
        Assert.AreEqual(2, callCount, "Loop must exit immediately upon achieving PASS without exhausting maxAttempts");
        Assert.AreEqual(HumanizerVerdict.Pass, result.Verdict);
    }

    [TestMethod]
    public async Task HU_06d_Bounded_Rewrite_Loop_Respects_CancellationToken()
    {
        string failingInitial = "We delve into solutions. We foster collaboration. We spearhead initiatives.";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
        {
            await _engine.EnsurePassingAsync(failingInitial, (p, ct) => Task.FromResult("rewrite"), 3, cts.Token);
        });
    }
}
