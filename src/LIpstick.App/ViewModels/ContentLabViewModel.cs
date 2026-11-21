using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick_App.ViewModels;

public partial class ContentLabViewModel : ObservableObject
{
    private readonly IContentLabService _contentLab;
    private readonly IHumanizerEngine _humanizer;
    private readonly IRubricEvaluator _rubricEvaluator;
    private readonly Dictionary<string, string[]> _savedInputs = new(StringComparer.Ordinal);

    [ObservableProperty]
    private List<SkillToolDefinition> _skills = new();

    [ObservableProperty]
    private SkillToolDefinition? _selectedSkill;

    [ObservableProperty]
    private string _inputOne = string.Empty;
    [ObservableProperty]
    private string _inputTwo = string.Empty;
    [ObservableProperty]
    private string _inputThree = string.Empty;
    [ObservableProperty]
    private string _inputFour = string.Empty;
    [ObservableProperty]
    private string _inputFive = string.Empty;

    [ObservableProperty]
    private string _inputOneHeader = "Input";
    [ObservableProperty]
    private string _inputTwoHeader = string.Empty;
    [ObservableProperty]
    private string _inputThreeHeader = string.Empty;
    [ObservableProperty]
    private string _inputFourHeader = string.Empty;
    [ObservableProperty]
    private string _inputFiveHeader = string.Empty;

    [ObservableProperty]
    private bool _hideInputTwo = true;
    [ObservableProperty]
    private bool _hideInputThree = true;
    [ObservableProperty]
    private bool _hideInputFour = true;
    [ObservableProperty]
    private bool _hideInputFive = true;

    [ObservableProperty]
    private string _resultText = string.Empty;
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    [ObservableProperty]
    private string _activeToolTitle = "Post";

    public ContentLabViewModel(IContentLabService contentLab, IHumanizerEngine humanizer, IRubricEvaluator rubricEvaluator)
    {
        _contentLab = contentLab;
        _humanizer = humanizer;
        _rubricEvaluator = rubricEvaluator;
    }

    public void Initialize()
    {
        Skills = _contentLab.GetSkillDefinitions().ToList();
        if (SelectedSkill is null && Skills.Count > 0)
            SelectedSkill = Skills[0];
    }

    partial void OnSelectedSkillChanging(SkillToolDefinition? value)
    {
        if (SelectedSkill is { } previous)
            _savedInputs[previous.Id] = [InputOne, InputTwo, InputThree, InputFour, InputFive];
    }

    partial void OnSelectedSkillChanged(SkillToolDefinition? value)
    {
        ConfigureInputs(value);
        if (value is not null && _savedInputs.TryGetValue(value.Id, out var saved))
        {
            InputOne = saved[0]; InputTwo = saved[1]; InputThree = saved[2]; InputFour = saved[3]; InputFive = saved[4];
        }
        else
        {
            InputOne = InputTwo = InputThree = InputFour = InputFive = string.Empty;
        }
        ResultText = string.Empty;
        StatusMessage = string.Empty;
    }

    private void ConfigureInputs(SkillToolDefinition? skill)
    {
        ActiveToolTitle = skill?.NavigationLabel ?? "Content tool";
        InputOneHeader = "Input"; InputTwoHeader = string.Empty; InputThreeHeader = string.Empty;
        InputFourHeader = string.Empty; InputFiveHeader = string.Empty;
        HideInputTwo = HideInputThree = HideInputFour = HideInputFive = true;

        switch (skill?.Id)
        {
            case "li-post":
                SetInputLabels("Post idea", "Audience", "Verified outcome or metric (optional)");
                break;
            case "li-carousel":
                SetInputLabels("Topic", "Audience", "Number of slides (8–12 recommended)", "Key takeaway", hideFive: true);
                break;
            case "li-comment":
                SetInputLabels("Original post", "Author name (optional)", "Author role (optional)", "Your perspective", "Comment archetype (optional)");
                break;
            case "li-reply":
                SetInputLabels("Your post topic", "Comments (one per line: name | role | comment)");
                break;
            case "li-profile":
                SetInputLabels("Current headline", "Current About section", "Experience descriptions (one per line)", "Has banner? (yes/no)", "Number of featured items");
                break;
            case "li-plan":
                SetInputLabels("Audience timezone", "Professional focus", "Content pillars (comma separated)");
                break;
            case "li-dm":
                SetInputLabels("Prospect name", "Prospect role", "Shared context you can truthfully reference");
                break;
            case "li-inbox":
                SetInputLabels("Messages (one per line: sender | title | message)");
                break;
            case "li-audit":
                SetInputLabels("Follower count", "Posts (one per line: topic | impressions | reactions | comments | reposts | profile clicks | format | day)");
                break;
            case "li-repurpose":
                SetInputLabels("Source title", "Source content", "Source format");
                break;
            case "li-human":
                SetInputLabels("Draft text to clean and score");
                break;
        }
    }

    private void SetInputLabels(string one, string? two = null, string? three = null, string? four = null, string? five = null, bool hideFive = false)
    {
        InputOneHeader = one;
        InputTwoHeader = two ?? string.Empty;
        InputThreeHeader = three ?? string.Empty;
        InputFourHeader = four ?? string.Empty;
        InputFiveHeader = five ?? string.Empty;
        HideInputTwo = two is null;
        HideInputThree = three is null;
        HideInputFour = four is null;
        HideInputFive = five is null || hideFive;
    }

    [RelayCommand]
    public void GenerateSelectedSkill()
    {
        ResultText = string.Empty;
        if (SelectedSkill is null)
        {
            StatusMessage = "Choose a tool first.";
            return;
        }

        try
        {
            ResultText = SelectedSkill.Id switch
            {
                "li-post" => GeneratePostDraft(),
                "li-carousel" => GenerateCarouselDraft(),
                "li-comment" => GenerateCommentDraft(),
                "li-reply" => GenerateReplyTriage(),
                "li-profile" => GenerateProfileAudit(),
                "li-plan" => GenerateWeeklyPlan(),
                "li-dm" => GenerateDmDraft(),
                "li-inbox" => GenerateInboxTriage(),
                "li-audit" => GenerateContentAudit(),
                "li-repurpose" => GenerateRepurposedPosts(),
                "li-human" => GenerateHumanizerReport(),
                _ => throw new InvalidOperationException("This tool is not available yet.")
            };
            if (string.IsNullOrWhiteSpace(StatusMessage))
                StatusMessage = "Draft ready. Verify every fact before using it.";
        }
        catch (FormatException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not run {SelectedSkill.NavigationLabel}: {ex.Message}";
        }
    }

    private string GeneratePostDraft()
    {
        Require(InputOne, "Add a post idea before drafting.");
        var result = _contentLab.DraftPost(new PostDraftRequest(InputOne.Trim(), InputTwo.Trim(), NullIfBlank(InputThree), null), _humanizer);
        StatusMessage = result.ValidationNotes.Count == 0
            ? $"Draft ready · Humanizer {result.HumanizerScore.OverallScore:F0} {result.HumanizerScore.Verdict}"
            : string.Join(" ", result.ValidationNotes);
        return result.FullCopyReadyPost;
    }

    private string GenerateCarouselDraft()
    {
        Require(InputOne, "Add a topic for the carousel.");
        int slides = ParseInt(InputThree, 8, "Enter a slide count from 3 to 12.");
        if (slides is < 3 or > 12) throw new FormatException("Choose between 3 and 12 slides.");
        var result = _contentLab.DraftCarousel(new CarouselDraftRequest(InputOne.Trim(), InputTwo.Trim(), slides, InputFour.Trim()), _humanizer);
        StatusMessage = string.Join(" ", result.ValidationNotes);
        return string.Join("\n\n", result.Slides.Select(s => $"Slide {s.SlideNumber} · {s.SlideType}\n{s.Headline}\n{s.Body}")) + $"\n\nAccompanying post\n{result.AccompanyingPostText}";
    }

    private string GenerateCommentDraft()
    {
        Require(InputOne, "Paste the original post to draft a relevant comment.");
        Require(InputFour, "Add your own perspective; do not invent an experience or statistic.");
        var result = _contentLab.DraftComment(new CommentDraftRequest(InputOne.Trim(), InputTwo.Trim(), InputThree.Trim(), NullIfBlank(InputFive), InputFour.Trim()), _humanizer);
        return string.Join("\n\n", result.Options.Select(o => $"{o.Archetype} · {o.SentenceCount} sentences\n{o.CommentText}")) + $"\n\nRecommendation\n{result.RecommendedOption}";
    }

    private string GenerateReplyTriage()
    {
        Require(InputTwo, "Add at least one comment, one per line as name | role | comment.");
        var comments = InputTwo.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((line, i) => ParseComment(line, i)).ToList();
        var result = _contentLab.TriageAndDraftReplies(new ReplyTriageRequest(InputOne.Trim(), comments), _humanizer);
        string counts = string.Join(" · ", result.BucketCounts.Select(kv => $"{kv.Key}: {kv.Value}"));
        return counts + "\n\n" + string.Join("\n\n", result.Replies.Select(r => $"{r.Bucket} · {r.CommentId}\n{r.DraftReplyText ?? "No reply suggested."}"));
    }

    private string GenerateProfileAudit()
    {
        var experiences = InputThree.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = _contentLab.AuditAndRewriteProfile(
            new ProfileAuditRequest(InputOne.Trim(), InputTwo.Trim(), experiences, InputFour.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase), ParseInt(InputFive, 0, "Featured item count must be a whole number.")),
            _rubricEvaluator,
            _humanizer);
        return $"Current rubric score: {result.CurrentScore}/100\n\nFix first\n{string.Join("\n", result.FixFirstList.Select(x => "• " + x))}\n\nDraft templates — replace bracketed prompts with verified information\n" +
               string.Join("\n\n", result.ProposedRewrites.Select(kv => $"{kv.Key}\n{kv.Value}"));
    }

    private string GenerateWeeklyPlan()
    {
        Require(InputOne, "Enter the audience timezone.");
        Require(InputTwo, "Enter the professional focus for the plan.");
        var pillars = InputThree.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = _contentLab.GenerateWeeklyPlan(new WeeklyPlanRequest(InputOne.Trim(), InputTwo.Trim(), pillars));
        return string.Join("\n", result.Schedule.Select(p => $"{p.Day} · {p.LocalTimeSlot} · {p.PostType}\n{p.Angle}\n{p.TopicOutline}")) +
               $"\n\nDaily engagement\n{string.Join("\n", result.EngagementRoutine)}\n\n{result.StrategyNotes}";
    }

    private string GenerateDmDraft()
    {
        Require(InputOne, "Enter the prospect's name.");
        Require(InputThree, "Enter a real shared context for the invitation.");
        var result = _contentLab.DraftDm(new DmDraftRequest(InputOne.Trim(), InputTwo.Trim(), InputThree.Trim(), true), _humanizer);
        StatusMessage = $"{result.CharacterCount}/200 characters · Humanizer {result.HumanizerScore.OverallScore:F0} {result.HumanizerScore.Verdict}";
        return result.DraftMessage;
    }

    private string GenerateInboxTriage()
    {
        Require(InputOne, "Add at least one message as sender | title | message, one per line.");
        var messages = InputOne.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((line, i) => ParseInboxMessage(line, i)).ToList();
        var result = _contentLab.TriageInbox(new InboxTriageRequest(messages), _humanizer);
        return string.Join("\n\n", result.TriagedItems.Select(item => $"{item.Bucket} · {item.MessageId}\n{item.TellDetected ?? "No automated-sales indicator detected."}\n{item.DraftReply ?? "No reply suggested."}"));
    }

    private string GenerateContentAudit()
    {
        int followers = ParseInt(InputOne, 0, "Follower count must be a whole number.");
        Require(InputTwo, "Add at least two post rows to compare.");
        var posts = InputTwo.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((line, i) => ParseAnalyticsPost(line, i)).ToList();
        if (posts.Count < 2) throw new ArgumentException("Add at least two post rows to compare.");
        var result = _contentLab.AuditContent(new AuditRequest(posts, followers));
        return $"Top performer\n{FormatPost(result.TopPerformers.FirstOrDefault())}\n\nLowest engagement\n{FormatPost(result.BottomPerformers.FirstOrDefault())}\n\nConsider stopping\n{string.Join("\n", result.StopDoing.Select(x => "• " + x))}\n\nConsider doing more\n{string.Join("\n", result.DoMore.Select(x => "• " + x))}";
    }

    private string GenerateRepurposedPosts()
    {
        Require(InputTwo, "Paste source content to extract posts from.");
        var result = _contentLab.ExtractAndRepurpose(new RepurposeRequest(InputOne.Trim(), InputTwo.Trim(), InputThree.Trim()), _humanizer);
        StatusMessage = result.IsThinAsset ? "Source is too thin to safely extract distinct posts; add more source material." : "Extracts are based on supplied source text. Verify context before use.";
        return result.IsThinAsset
            ? "No drafts generated. Provide at least 100 characters of source with verifiable details or a described mistake."
            : string.Join("\n\n", result.ProposedWeekPosts);
    }

    private string GenerateHumanizerReport()
    {
        Require(InputOne, "Paste text to clean and score.");
        var (cleaned, receipt) = _humanizer.CleanText(InputOne);
        var score = _humanizer.Evaluate(cleaned);
        StatusMessage = $"{score.OverallScore:F0} {score.Verdict} · Cleaned text remains editable.";
        var changes = receipt.Invisible.Select(x => $"Removed {x.Count} {x.Name}")
            .Concat(receipt.Typographic.Select(x => $"Normalized {x.Count} {x.Name}"))
            .Concat(receipt.Lexical.Select(x => $"Replaced {x.Count} instances of '{x.Find}'"))
            .Concat(receipt.Structures.Select(x => $"Flagged {x.Count} {x.Name}"))
            .ToList();
        return $"Cleaned draft\n{cleaned}\n\nHumanizer score: {score.OverallScore:F0} · {score.Verdict}\nBurstiness {score.BurstinessScore:F0} · Specificity {score.SpecificityScore:F0} · Slop {score.SlopDensityScore:F0} · Fingerprint {score.FingerprintScore:F0} · Voice {score.VoiceScore:F0}\n\nChanges\n{(changes.Count == 0 ? "No cleaning changes." : string.Join("\n", changes.Select(x => "• " + x)))}";
    }

    private static CommentItem ParseComment(string line, int index)
    {
        var parts = line.Split('|', 3, StringSplitOptions.TrimEntries);
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
            throw new FormatException($"Comment line {index + 1} must use name | role | comment.");
        return new CommentItem($"comment-{index + 1}", parts[0], parts[1], parts[2]);
    }

    private static InboxMessageItem ParseInboxMessage(string line, int index)
    {
        var parts = line.Split('|', 3, StringSplitOptions.TrimEntries);
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
            throw new FormatException($"Message line {index + 1} must use sender | title | message.");
        bool hasCalendarLink = parts[2].Contains("calendly.com", StringComparison.OrdinalIgnoreCase);
        return new InboxMessageItem($"message-{index + 1}", parts[0], parts[1], parts[2], DateTime.UtcNow, hasCalendarLink, 0);
    }

    private static PostAnalyticsItem ParseAnalyticsPost(string line, int index)
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length < 8)
            throw new FormatException($"Post row {index + 1} needs topic | impressions | reactions | comments | reposts | profile clicks | format | day.");
        if (!int.TryParse(parts[1], out int impressions) || !int.TryParse(parts[2], out int reactions) ||
            !int.TryParse(parts[3], out int comments) || !int.TryParse(parts[4], out int reposts) ||
            !int.TryParse(parts[5], out int clicks) || !Enum.TryParse(parts[7], true, out DayOfWeek day))
            throw new FormatException($"Post row {index + 1} contains an invalid number or day.");
        return new PostAnalyticsItem($"post-{index + 1}", string.Empty, parts[6], parts[0], impressions, reactions, comments, reposts, clicks, day);
    }

    private static string FormatPost(PostAnalyticsItem? post) => post is null
        ? "No result."
        : $"{post.Topic}\n{post.Format} · {post.Day} · {post.Impressions} impressions · {post.Reactions} reactions · {post.Comments} comments · {post.Reposts} reposts";

    private static void Require(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(message);
    }

    private static int ParseInt(string value, int defaultValue, string errorMessage) =>
        string.IsNullOrWhiteSpace(value) ? defaultValue : int.TryParse(value, out int parsed) ? parsed : throw new FormatException(errorMessage);

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
