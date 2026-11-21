using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Core.Services;

public class HumanizerEngine : IHumanizerEngine
{
    private static readonly Regex UrlRegex = new(@"https?://\S+|www\.\S+|\S+@\S+\.\S+", RegexOptions.Compiled);
    private static readonly Regex SentRegex = new(@"[^.!?\n]+[.!?]*", RegexOptions.Compiled);
    private static readonly Regex WordRegex = new(@"[A-Za-z']+", RegexOptions.Compiled);
    private static readonly Regex ContractionsRegex = new(@"\b\w+'(?:s|t|re|ve|ll|d|m)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PronounsRegex = new(@"\b(i|me|my|mine|we|us|our|you|your)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NumbersRegex = new(@"\b\d[\d,.]*%?\b|\$\d", RegexOptions.Compiled);
    private static readonly Regex ProperRegex = new(@"(?<![.!?]\s)(?<!^)\b[A-Z][a-z]{2,}\b", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex BulletRegex = new(@"(?m)^\s*[-*•]\s+(.+)$", RegexOptions.Compiled);

    private readonly List<SlopEntry> _words = new();
    private readonly List<SlopEntry> _phrases = new();
    private readonly List<SlopStructure> _structures = new();
    private readonly List<InvisibleEntry> _invisible = new();

    public HumanizerEngine(string? slopJsonContent = null)
    {
        InitializeLexicon(slopJsonContent);
    }

    private void InitializeLexicon(string? slopJsonContent)
    {
        if (string.IsNullOrWhiteSpace(slopJsonContent))
        {
            // Default essential slop terms if json not provided
            var defaultWords = new[]
            {
                ("delve", "look into", "stock-verb"),
                ("spearhead", "led", "corporate-buzz"),
                ("seamless", "smooth", "hype"),
                ("testament", "proof", "stock-metaphor"),
                ("realm", "world", "stock-noun"),
                ("synergy", "working together", "buzzword"),
                ("tapestry", "mix", "metaphor"),
                ("pivotal", "key", "hype"),
                ("beacon", "model", "metaphor"),
                ("foster", "build", "stock-verb"),
                ("revolutionize", "change", "hype")
            };
            foreach (var (f, r, fam) in defaultWords)
            {
                _words.Add(new SlopEntry(f, r, fam));
            }

            var defaultPhrases = new[]
            {
                ("in the realm of", "in", "filler"),
                ("a testament to", "proof of", "cliche"),
                ("game changer", "turning point", "cliche"),
                ("at the end of the day", "ultimately", "filler"),
                ("moving forward", "next", "corporate-filler")
            };
            foreach (var (f, r, fam) in defaultPhrases)
            {
                _phrases.Add(new SlopEntry(f, r, fam));
            }

            _invisible.Add(new InvisibleEntry("U+200B", "ZERO WIDTH SPACE", "delete"));
            _invisible.Add(new InvisibleEntry("U+200C", "ZERO WIDTH NON-JOINER", "delete"));
            _invisible.Add(new InvisibleEntry("U+200D", "ZERO WIDTH JOINER", "delete"));
            _invisible.Add(new InvisibleEntry("U+2060", "WORD JOINER", "delete"));
            _invisible.Add(new InvisibleEntry("U+FEFF", "ZERO WIDTH NO-BREAK SPACE / BOM", "delete"));
            _invisible.Add(new InvisibleEntry("U+00AD", "SOFT HYPHEN", "delete"));
            _invisible.Add(new InvisibleEntry("U+180E", "MONGOLIAN VOWEL SEPARATOR", "delete"));
            _invisible.Add(new InvisibleEntry("U+061C", "ARABIC LETTER MARK", "delete"));
            _invisible.Add(new InvisibleEntry("U+200E", "LEFT-TO-RIGHT MARK", "delete"));
            _invisible.Add(new InvisibleEntry("U+200F", "RIGHT-TO-LEFT MARK", "delete"));

            _structures.Add(new SlopStructure("rule-of-three", "Rule of three adjectives", @"(?i)\b\w+,\s+\w+,\s+and\s+\w+\b", "Avoid triple adjective piles. Pick the single strongest word."));
            _structures.Add(new SlopStructure("not-only-but", "Not only X but also Y", @"(?i)\bnot only\b.+?\bbut (also)?\b", "State the point directly without 'not only... but also'."));
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(slopJsonContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("invisible", out var invElem))
            {
                foreach (var item in invElem.EnumerateArray())
                {
                    _invisible.Add(new InvisibleEntry(
                        item.GetProperty("cp").GetString() ?? "",
                        item.GetProperty("name").GetString() ?? "",
                        item.GetProperty("action").GetString() ?? "delete"
                    ));
                }
            }

            if (root.TryGetProperty("words", out var wordsElem))
            {
                foreach (var item in wordsElem.EnumerateArray())
                {
                    _words.Add(new SlopEntry(
                        item.GetProperty("find").GetString() ?? "",
                        item.TryGetProperty("replace", out var r) ? r.GetString() ?? "" : "",
                        item.TryGetProperty("family", out var fam) ? fam.GetString() ?? "" : ""
                    ));
                }
            }

            if (root.TryGetProperty("phrases", out var phrasesElem))
            {
                foreach (var item in phrasesElem.EnumerateArray())
                {
                    _phrases.Add(new SlopEntry(
                        item.GetProperty("find").GetString() ?? "",
                        item.TryGetProperty("replace", out var r) ? r.GetString() ?? "" : "",
                        item.TryGetProperty("family", out var fam) ? fam.GetString() ?? "" : ""
                    ));
                }
            }

            if (root.TryGetProperty("structures", out var structElem))
            {
                foreach (var item in structElem.EnumerateArray())
                {
                    _structures.Add(new SlopStructure(
                        item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        item.GetProperty("name").GetString() ?? "",
                        item.GetProperty("regex").GetString() ?? "",
                        item.TryGetProperty("fix", out var fix) ? fix.GetString() ?? "" : ""
                    ));
                }
            }
        }
        catch
        {
            // Fallback gracefully
        }
    }

    public (string CleanedText, HumanizeReport Report) CleanText(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return (string.Empty, new HumanizeReport(Array.Empty<InvisibleHit>(), Array.Empty<TypoHit>(), Array.Empty<LexicalHit>(), Array.Empty<StructureFlag>()));
        }

        // 1. Protect URLs
        var (textWithPlaceholders, urls) = ProtectUrls(input);

        // 2. Pass 1: Invisible characters
        var (p1Text, invHits) = PassInvisible(textWithPlaceholders);

        // 3. Pass 2: Typographic characters
        var (p2Text, typoHits) = PassTypographic(p1Text);

        // 4. Pass 3: Lexical slop scrubbing
        var (p3Text, lexicalHits) = PassLexical(p2Text);

        // 5. Restore URLs
        var restoredText = RestoreUrls(p3Text, urls).Trim();

        // 6. Scan structures
        var structureFlags = ScanStructures(restoredText);

        var report = new HumanizeReport(invHits, typoHits, lexicalHits, structureFlags);
        return (restoredText, report);
    }

    public HumanizerResult Evaluate(string text, string? referenceVoice = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HumanizerResult(
                CleanedText: string.Empty,
                OverallScore: 0.0,
                Verdict: HumanizerVerdict.Flagged,
                BurstinessScore: 0.0,
                SpecificityScore: 0.0,
                SlopDensityScore: 0.0,
                FingerprintScore: 0.0,
                VoiceScore: 0.0,
                FlaggedStructuralTells: Array.Empty<string>(),
                Report: new HumanizeReport(Array.Empty<InvisibleHit>(), Array.Empty<TypoHit>(), Array.Empty<LexicalHit>(), Array.Empty<StructureFlag>())
            );
        }

        var (cleanedText, report) = CleanText(text);

        var burstiness = CheckBurstiness(text);
        var specificity = CheckSpecificity(text);
        var slop = CheckSlop(text);
        var fingerprint = CheckFingerprint(text);
        var voice = CheckVoice(text);

        var scores = new[] { burstiness.Score, specificity.Score, slop.Score, fingerprint.Score, voice.Score };
        var mean = scores.Average();
        var min = scores.Min();

        // The weakest check drags the verdict: overall = mean * 0.6 + min * 0.4
        var overall = Math.Round((mean * 0.6) + (min * 0.4), 1);

        HumanizerVerdict verdict;
        if (overall >= 70.0 && min >= 55.0)
        {
            verdict = HumanizerVerdict.Pass;
        }
        else if (overall >= 50.0)
        {
            verdict = HumanizerVerdict.Review;
        }
        else
        {
            verdict = HumanizerVerdict.Flagged;
        }

        var flaggedTells = report.Structures.Select(s => s.Name).ToList();

        return new HumanizerResult(
            CleanedText: cleanedText,
            OverallScore: overall,
            Verdict: verdict,
            BurstinessScore: Math.Round(burstiness.Score, 1),
            SpecificityScore: Math.Round(specificity.Score, 1),
            SlopDensityScore: Math.Round(slop.Score, 1),
            FingerprintScore: Math.Round(fingerprint.Score, 1),
            VoiceScore: Math.Round(voice.Score, 1),
            FlaggedStructuralTells: flaggedTells,
            Report: report
        );
    }

    public async Task<HumanizerResult> EnsurePassingAsync(
        string initialText,
        Func<string, CancellationToken, Task<string>> rewriteFunc,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        var currentText = initialText;
        var eval = Evaluate(currentText);

        for (int i = 0; i < maxAttempts && eval.Verdict != HumanizerVerdict.Pass; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var promptInstruction = $"Improve this draft to sound natural, human, and direct. Eliminate all AI buzzwords, vary sentence lengths, and add concrete proof markers: {currentText}";
            var candidate = await rewriteFunc(promptInstruction, cancellationToken);
            if (string.IsNullOrWhiteSpace(candidate))
                break;

            currentText = candidate;
            eval = Evaluate(currentText);
        }

        return eval;
    }

    #region Passes

    private static (string, List<string>) ProtectUrls(string text)
    {
        var urls = new List<string>();
        var result = UrlRegex.Replace(text, m =>
        {
            urls.Add(m.Value);
            return $"\0URL{urls.Count - 1}\0";
        });
        return (result, urls);
    }

    private static string RestoreUrls(string text, List<string> urls)
    {
        for (int i = 0; i < urls.Count; i++)
        {
            text = text.Replace($"\0URL{i}\0", urls[i]);
        }
        return text;
    }

    private (string, List<InvisibleHit>) PassInvisible(string text)
    {
        var hits = new List<InvisibleHit>();

        // Explicit list of invisible format codes
        var invisibleChars = new (char c, string code, string name)[]
        {
            ('\u200B', "U+200B", "ZERO WIDTH SPACE"),
            ('\u200C', "U+200C", "ZERO WIDTH NON-JOINER"),
            ('\u200D', "U+200D", "ZERO WIDTH JOINER"),
            ('\u2060', "U+2060", "WORD JOINER"),
            ('\uFEFF', "U+FEFF", "ZERO WIDTH NO-BREAK SPACE / BOM"),
            ('\u00AD', "U+00AD", "SOFT HYPHEN"),
            ('\u180E', "U+180E", "MONGOLIAN VOWEL SEPARATOR"),
            ('\u061C', "U+061C", "ARABIC LETTER MARK"),
            ('\u200E', "U+200E", "LEFT-TO-RIGHT MARK"),
            ('\u200F', "U+200F", "RIGHT-TO-LEFT MARK")
        };

        foreach (var (c, code, name) in invisibleChars)
        {
            int count = text.Count(ch => ch == c);
            if (count > 0)
            {
                hits.Add(new InvisibleHit($"{code} {name}", count, "delete"));
                text = text.Replace(c.ToString(), string.Empty);
            }
        }

        // Clean any other Cf unicode categories
        var cfList = text.Where(c => char.GetUnicodeCategory(c) == UnicodeCategory.Format).ToList();
        if (cfList.Count > 0)
        {
            hits.Add(new InvisibleHit("other invisible format chars", cfList.Count, "delete"));
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (char.GetUnicodeCategory(ch) != UnicodeCategory.Format)
                {
                    sb.Append(ch);
                }
            }
            text = sb.ToString();
        }

        return (text, hits);
    }

    private (string, List<TypoHit>) PassTypographic(string text)
    {
        var hits = new List<TypoHit>();

        // Em dash —
        int emCount = text.Count(c => c == '—');
        if (emCount > 0)
        {
            hits.Add(new TypoHit("— EM DASH", emCount, ", "));
            text = Regex.Replace(text, @"\s*—\s*", ", ");
        }

        // En dash –
        int enCount = text.Count(c => c == '–');
        if (enCount > 0)
        {
            hits.Add(new TypoHit("– EN DASH", enCount, "-"));
            text = Regex.Replace(text, @"\s*–\s*(?=\d)", "-");
            text = Regex.Replace(text, @"\s+–\s+", ", ");
            text = text.Replace("–", "-");
        }

        // Curly double quotes
        int curlyDoubleCount = text.Count(c => c == '“' || c == '”');
        if (curlyDoubleCount > 0)
        {
            hits.Add(new TypoHit("“” CURLY DOUBLE QUOTES", curlyDoubleCount, "\""));
            text = text.Replace('“', '"').Replace('”', '"');
        }

        // Curly single quotes
        int curlySingleCount = text.Count(c => c == '‘' || c == '’');
        if (curlySingleCount > 0)
        {
            hits.Add(new TypoHit("‘’ CURLY SINGLE QUOTES", curlySingleCount, "'"));
            text = text.Replace('‘', '\'').Replace('’', '\'');
        }

        // Ellipsis …
        int ellipCount = text.Count(c => c == '…');
        if (ellipCount > 0)
        {
            hits.Add(new TypoHit("… HORIZONTAL ELLIPSIS", ellipCount, "..."));
            text = text.Replace("…", "...");
        }

        // Hard / non-breaking spaces
        var nbspChars = new[] { '\u00A0', '\u202F', '\u2009' };
        int nbspCount = text.Count(c => nbspChars.Contains(c));
        if (nbspCount > 0)
        {
            hits.Add(new TypoHit("NO-BREAK / THIN SPACE", nbspCount, " "));
            foreach (var ch in nbspChars)
                text = text.Replace(ch, ' ');
        }

        // Clean up punctuation splices: ", ." or ", !" -> "." or "!"
        text = Regex.Replace(text, @",\s*([,.;:!?])", "$1");
        text = Regex.Replace(text, @",\s*\n", "\n");

        return (text, hits);
    }

    private (string, List<LexicalHit>) PassLexical(string text)
    {
        var hits = new List<LexicalHit>();

        var allEntries = _phrases.Concat(_words)
            .OrderByDescending(e => e.Find.Length)
            .ToList();

        foreach (var entry in allEntries)
        {
            var pattern = new Regex(@"\b" + Regex.Escape(entry.Find).Replace(@"\ ", @"\s+") + @"\b", RegexOptions.IgnoreCase);
            var matches = pattern.Matches(text);
            if (matches.Count > 0)
            {
                hits.Add(new LexicalHit(entry.Find, entry.Replace, matches.Count, entry.Family));
                text = pattern.Replace(text, m => MatchCase(m.Value, entry.Replace));
            }
        }

        // Clean extra spacing
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"(?m)^[ \t]*([,.;:])\s*", string.Empty);
        text = Regex.Replace(text, @"\s+([,.;:!?])", "$1");
        text = Regex.Replace(text, @"(?m)^[ \t]+$", string.Empty);
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return (text, hits);
    }

    private List<StructureFlag> ScanStructures(string text)
    {
        var flags = new List<StructureFlag>();
        foreach (var s in _structures)
        {
            try
            {
                var pattern = new Regex(s.Regex, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                var count = pattern.Matches(text).Count;
                if (count > 0)
                {
                    flags.Add(new StructureFlag(s.Name, count, s.Fix));
                }
            }
            catch
            {
                // Ignore invalid regex
            }
        }
        return flags;
    }

    private static string MatchCase(string source, string replacement)
    {
        if (string.IsNullOrEmpty(replacement))
            return string.Empty;

        if (source.All(char.IsUpper) && source.Length > 1)
            return replacement.ToUpperInvariant();

        if (char.IsUpper(source[0]))
            return char.ToUpperInvariant(replacement[0]) + replacement[1..];

        return replacement;
    }

    #endregion

    #region Heuristics

    private static double Clamp(double value) => Math.Max(0.0, Math.Min(100.0, value));

    private static double Scale(double value, double human, double machine)
    {
        if (Math.Abs(human - machine) < 0.0001)
            return 50.0;
        return Clamp((value - machine) / (human - machine) * 100.0);
    }

    private static List<string> GetSentences(string text)
    {
        return SentRegex.Matches(text)
            .Select(m => m.Value.Trim())
            .Where(s => s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > 2)
            .ToList();
    }

    private static List<string> GetWords(string text)
    {
        return WordRegex.Matches(text).Select(m => m.Value).ToList();
    }

    public CheckScore CheckBurstiness(string text)
    {
        var sentences = GetSentences(text);
        if (sentences.Count < 4)
        {
            return new CheckScore(70.0, "short text (neutral)");
        }

        var lens = sentences.Select(s => (double)s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length).ToList();
        var mean = lens.Average();
        if (mean == 0) return new CheckScore(70.0, "empty sentences");

        var variance = lens.Select(val => Math.Pow(val - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);
        var cv = stdDev / mean;

        var score = Scale(cv, human: 0.70, machine: 0.22);
        return new CheckScore(score, $"variation {cv:F2} across {sentences.Count} sentences (want 0.55+)");
    }

    public CheckScore CheckSpecificity(string text)
    {
        var words = GetWords(text);
        if (words.Count == 0)
        {
            return new CheckScore(50.0, "empty");
        }

        double per100 = 100.0 / words.Count;
        int numbers = NumbersRegex.Matches(text).Count;
        int properNouns = ProperRegex.Matches(text).Select(m => m.Value).Distinct().Count();
        int hits = numbers + properNouns;
        double density = hits * per100;

        if (words.Count < 25)
        {
            // For short snippets like headlines, award high points if concrete numbers exist
            if (hits > 0)
            {
                var shortScore = Scale(density, human: 6.0, machine: 0.5);
                return new CheckScore(shortScore, $"{hits} concrete markers in short text");
            }
            return new CheckScore(70.0, "short text (neutral)");
        }

        var score = Scale(density, human: 6.0, machine: 0.5);
        return new CheckScore(score, $"{hits} concrete markers, {density:F1} per 100 words (want 4+)");
    }

    public CheckScore CheckSlop(string text)
    {
        var words = GetWords(text);
        if (words.Count == 0)
        {
            return new CheckScore(50.0, "empty");
        }

        int hits = 0;
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allEntries = _phrases.Concat(_words);
        foreach (var entry in allEntries)
        {
            var pattern = new Regex(@"\b" + Regex.Escape(entry.Find).Replace(@"\ ", @"\s+") + @"\b", RegexOptions.IgnoreCase);
            int count = pattern.Matches(text).Count;
            if (count > 0)
            {
                hits += count;
                found.Add(entry.Find);
            }
        }

        double density = (double)hits * 100.0 / words.Count;
        var score = Scale(density, human: 0.0, machine: 4.0);

        var detail = $"{hits} stock terms, {density:F1} per 100 words";
        if (found.Count > 0)
        {
            var sample = string.Join(", ", found.Take(4));
            detail += $" ({sample})";
        }

        return new CheckScore(score, detail);
    }

    public CheckScore CheckFingerprint(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new CheckScore(100.0, "0 invisible");

        int invisible = text.Count(c => char.GetUnicodeCategory(c) == UnicodeCategory.Format);
        int em = text.Count(c => c == '—');
        int curly = text.Count(c => "‘’“”".Contains(c));
        int ellip = text.Count(c => c == '…');
        int nbsp = text.Count(c => "\u00A0\u202F\u2009".Contains(c));

        int total = (invisible * 4) + (em * 2) + curly + ellip + nbsp;
        double per1k = (double)total * 1000.0 / Math.Max(text.Length, 1);
        var score = Scale(per1k, human: 0.0, machine: 12.0);

        var detail = $"{invisible} invisible, {em} em dash, {curly} curly quote, {ellip} ellipsis, {nbsp} hard space";
        return new CheckScore(score, detail);
    }

    public CheckScore CheckVoice(string text)
    {
        var words = GetWords(text);
        if (words.Count < 25)
        {
            int tellsShort = 0;
            foreach (var s in _structures)
            {
                try
                {
                    if (new Regex(s.Regex, RegexOptions.Multiline | RegexOptions.IgnoreCase).IsMatch(text))
                        tellsShort++;
                }
                catch { }
            }
            double shortVoice = tellsShort > 0 ? Math.Max(0.0, 70.0 - tellsShort * 20.0) : 75.0;
            return new CheckScore(shortVoice, "short text voice");
        }

        double per100 = 100.0 / words.Count;
        double contractions = ContractionsRegex.Matches(text).Count * per100;
        double person = PronounsRegex.Matches(text).Count * per100;

        int tells = 0;
        var names = new List<string>();
        foreach (var s in _structures)
        {
            try
            {
                int count = new Regex(s.Regex, RegexOptions.Multiline | RegexOptions.IgnoreCase).Matches(text).Count;
                if (count > 0)
                {
                    tells += count;
                    names.Add(s.Id);
                }
            }
            catch { }
        }

        var bulletMatches = BulletRegex.Matches(text);
        bool uniform = false;
        if (bulletMatches.Count >= 3)
        {
            var lens = bulletMatches.Select(m => (double)m.Groups[1].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length).ToList();
            var mean = lens.Average();
            var stdDev = Math.Sqrt(lens.Select(v => Math.Pow(v - mean, 2)).Average());
            if (stdDev < 1.6)
            {
                uniform = true;
                names.Add("uniform-bullets");
            }
        }

        double score = (Scale(contractions, human: 3.0, machine: 0.0) * 0.35)
                     + (Scale(person, human: 8.0, machine: 1.0) * 0.35)
                     + (Clamp(100.0 - (tells * 22.0)) * 0.30);

        if (uniform)
        {
            score -= 12.0;
        }

        var detail = $"{contractions:F1} contractions, {person:F1} pronouns per 100 words, {tells} tells";
        if (names.Count > 0)
        {
            detail += $" [{string.Join(", ", names.Take(4))}]";
        }

        return new CheckScore(Clamp(score), detail);
    }

    #endregion

    private record SlopEntry(string Find, string Replace, string Family);
    private record SlopStructure(string Id, string Name, string Regex, string Fix);
    private record InvisibleEntry(string Cp, string Name, string Action);
}
