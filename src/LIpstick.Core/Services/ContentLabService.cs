using System.Text.RegularExpressions;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Core.Services;

public class ContentLabService : IContentLabService
{
    private static readonly Regex OutboundLinkRegex = new(@"https?://\S+", RegexOptions.Compiled);
    private static readonly Regex HashtagRegex = new(@"#\w+", RegexOptions.Compiled);
    private static readonly Regex PlaceholderRegex = new(@"\{\{.*?\}\}", RegexOptions.Compiled);

    private readonly List<SkillToolDefinition> _skills = new()
    {
        new("li-post", "Feed Post Author", "Content Creation", "Author high-engagement LinkedIn feed posts with proven hook formulas", new[] { "Topic", "Audience", "Specific Metric" }),
        new("li-carousel", "Carousel & PDF Compiler", "Content Creation", "Structure slide-by-slide educational decks ready for PDF export", new[] { "Topic", "Number of Slides", "Key Takeaway" }),
        new("li-comment", "9-Archetype Comment Engine", "Engagement", "Draft high-value top-level comments that add real perspective", new[] { "Original Post", "Archetype", "Your Perspective" }),
        new("li-reply", "Thread Triage & Reply", "Engagement", "Filter spam and generate contextual replies to comments on your posts", new[] { "Comment Text", "Author Role" }),
        new("li-profile", "Profile Audit & Rewrite", "Profile Polish", "Audit and rewrite headline, summary, and experience sections", new[] { "Profile Section", "Current Text" }),
        new("li-plan", "Weekly Calendar & 5/3/2", "Strategy", "4x weekly publishing strategy and 10-person daily engagement list", new[] { "Timezone", "Professional Focus" }),
        new("li-dm", "Invite & DM Outreach", "Networking", "200-char connection invites and 2-step value follow-ups", new[] { "Prospect Name", "Prospect Role", "Shared Context" }),
        new("li-inbox", "Message Triage & Bot Filter", "Networking", "Detect automated sales pitch bots and flag high-priority conversations", new[] { "Incoming Message" }),
        new("li-audit", "Post-Mortem Analytics", "Analytics", "Analyze post performance to identify top formats and dead angles", new[] { "Post URL or Metrics" }),
        new("li-repurpose", "Long-Form Extraction", "Content Creation", "Extract 3-5 distinct LinkedIn posts from blogs, whitepapers, or videos", new[] { "Source Text" }),
        new("li-human", "AI Cleaner & Scorecard", "Quality Control", "Audit and strip invisible AI watermarks and stock vocabulary", new[] { "Draft Text" })
    };

    public IReadOnlyList<SkillToolDefinition> GetSkillDefinitions() => _skills;

    public PostDraftResult DraftPost(PostDraftRequest request, IHumanizerEngine humanizer)
    {
        var validationNotes = new List<string>();

        if (string.IsNullOrWhiteSpace(request.TopicOrIdea))
        {
            validationNotes.Add("Topic or idea is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SpecificMetric))
        {
            validationNotes.Add("No verified metric or specific outcome was supplied. Keep the draft qualitative or add a result you can substantiate.");
        }

        string topic = request.TopicOrIdea.Trim();
        string audience = string.IsNullOrWhiteSpace(request.Audience) ? "your audience" : request.Audience.Trim();
        var hookCandidates = new List<string>
        {
            $"For {audience}, {topic} is easier to improve when the current steps are visible.",
            $"A useful starting point for {topic}: identify what happens, who does it, and where work stalls.",
            $"Before changing {topic}, decide which outcome you can measure and what evidence would support it."
        };

        string selectedHook = hookCandidates[0];
        string measuredResult = string.IsNullOrWhiteSpace(request.SpecificMetric)
            ? string.Empty
            : $"\n\nA result from your own measurement: {request.SpecificMetric.Trim()}. Include it only if you can verify the source and timeframe.";
        string body = $@"A useful discussion of {topic} starts with the process as it actually works. Separate observed behavior from assumptions before choosing a change.

1. Map the steps people follow today.
2. Mark handoffs, repeated work, and decisions that need context.
3. Choose one small change that you can evaluate safely.
4. Compare the result with a baseline you recorded.

The audience and constraints matter. An approach that fits one team may not fit another, so describe trade-offs you can support instead of presenting one solution as universal.{measuredResult}

What part of {topic} would you examine first?";

        string fullPost = $"{selectedHook}\n\n{body}\n\n#process #work";

        // T6: Gate against link in post body
        if (OutboundLinkRegex.IsMatch(fullPost))
        {
            validationNotes.Add("LinkedIn algorithm penalizes outbound links in post text. Move links to the first comment.");
        }

        // Check hashtags count (max 3)
        var hashtagMatches = HashtagRegex.Matches(fullPost);
        if (hashtagMatches.Count > 3)
        {
            validationNotes.Add($"Found {hashtagMatches.Count} hashtags. Limit to maximum 3 hashtags.");
        }

        var eval = humanizer.Evaluate(fullPost);

        return new PostDraftResult(
            HookCandidates: hookCandidates,
            SelectedHook: selectedHook,
            BodyDraft: body,
            FullCopyReadyPost: fullPost,
            HumanizerScore: eval,
            ValidationNotes: validationNotes
        );
    }

    public WeeklyPlanResult GenerateWeeklyPlan(WeeklyPlanRequest request)
    {
        var schedule = new List<ScheduledPost>
        {
            new(DayOfWeek.Monday, "Case Study / Proof", "Show concrete numbers from last week", "Hook-01", "08:15 AM", $"Case study breakdown for {request.ProfessionalDomain}"),
            new(DayOfWeek.Tuesday, "Contrarian / Opinion", "Challenge an accepted myth in your niche", "Hook-07", "08:30 AM", $"Why standard advice in {request.ProfessionalDomain} fails"),
            new(DayOfWeek.Thursday, "Framework / How-To", "Step-by-step checklist or template", "Hook-12", "09:00 AM", "3-step actionable framework with bulleted execution"),
            new(DayOfWeek.Friday, "Personal Lesson / Retrospective", "What went wrong and what we learned", "Hook-19", "08:45 AM", "A costly mistake and the principle that resolved it")
        };

        var engagement = new List<string>
        {
            "5 industry peers to leave substantive comments on before posting",
            "3 ideal clients / prospects to view and engage with thoughtfully",
            "2 creators outside your direct bubble to cross-pollinate perspectives"
        };

        string strategyNotes = $"Optimized for {request.AudienceTimezone} peak morning feeds. No back-to-back duplicate formats. 5/3/2 engagement rule applied daily.";

        return new WeeklyPlanResult(schedule, engagement, strategyNotes);
    }

    public DmDraftResult DraftDm(DmDraftRequest request, IHumanizerEngine humanizer)
    {
        string draft;
        if (request.IsInviteNote)
        {
            // 200 character hard limit on LinkedIn connection requests
            draft = $"Hi {request.ProspectName}, {request.MutualContext}. I would be glad to connect.";
            if (draft.Length > 200)
            {
                draft = draft[..197] + "...";
            }
        }
        else
        {
            draft = $"Hi {request.ProspectName},\n\nI wanted to follow up on {request.MutualContext}. If this is relevant to your work as {request.ProspectRole}, what would be useful to discuss?";
        }

        var eval = humanizer.Evaluate(draft);
        bool withinLimit = request.IsInviteNote ? draft.Length <= 200 : draft.Length <= 600;

        return new DmDraftResult(draft, draft.Length, withinLimit, eval);
    }

    public CarouselDraftResult DraftCarousel(CarouselDraftRequest request, IHumanizerEngine humanizer)
    {
        var notes = new List<string>();
        if (request.SlideCount < 8)
        {
            notes.Add("Slide count is under 8. Recommend a single text post instead of a carousel.");
        }
        else if (request.SlideCount > 12)
        {
            notes.Add("Slide count exceeds 12. Completion rate drops noticeably beyond 12 slides.");
        }

        int slideCount = Math.Clamp(request.SlideCount, 4, 12);
        string topic = string.IsNullOrWhiteSpace(request.Topic) ? "Your topic" : request.Topic.Trim();
        string audience = string.IsNullOrWhiteSpace(request.Audience) ? "your audience" : request.Audience.Trim();
        string takeaway = string.IsNullOrWhiteSpace(request.KeyTakeaway)
            ? "Add the main takeaway you can support from your source material."
            : request.KeyTakeaway.Trim();
        string coverTitle = string.Join(' ', topic.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6));
        var slides = new List<CarouselSlide>
        {
            new(1, "Cover", coverTitle, $"A concise guide for {audience}."),
            new(2, "The Stake", "Why this matters", $"Context to explain: {takeaway}")
        };

        int recapNumber = slideCount - 1;
        for (int i = 3; i < recapNumber; i++)
        {
            slides.Add(new CarouselSlide(i, "Content", $"{topic} Â· Part {i - 2}", $"Use this slide for one verified detail, step, example, or trade-off about {topic}."));
        }

        slides.Add(new CarouselSlide(recapNumber, "Recap", "Key takeaway", takeaway));
        slides.Add(new CarouselSlide(slideCount, "CTA", "Next step", "Choose one relevant action for the reader to take next."));
        string postText = $"A short carousel for {audience} about {topic}.\nKey takeaway: {takeaway}";
        return new CarouselDraftResult(slides, postText, notes);
    }

    public CommentDraftResult DraftComment(CommentDraftRequest request, IHumanizerEngine humanizer)
    {
        string perspective = string.IsNullOrWhiteSpace(request.YourPerspective)
            ? "Add your own perspective before drafting."
            : request.YourPerspective.Trim().TrimEnd('.', '!', '?');
        string excerpt = request.PostText.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
            ?? "the post";
        if (excerpt.Length > 100) excerpt = excerpt[..97] + "...";
        var options = new List<CommentOption>
        {
            new("Add perspective", $"{perspective}. What conditions would change the trade-off?", 2),
            new("Ask a follow-up", $"The post says: â€œ{excerpt}â€. How do you see that alongside this perspective: {perspective}?", 2)
        };

        return new CommentDraftResult(options, "Choose the option that best reflects your view and edit it to sound like you.");
    }

    public ReplyTriageResult TriageAndDraftReplies(ReplyTriageRequest request, IHumanizerEngine humanizer)
    {
        var counts = new Dictionary<string, int>
        {
            { "LEAD", 0 }, { "SUBSTANCE", 0 }, { "PEER", 0 }, { "SUPPORT", 0 }, { "NOISE", 0 }
        };

        var replies = new List<ReplyTriageItem>();
        foreach (var c in request.Comments)
        {
            string bucket;
            string? replyText = null;

            if (c.Text.Contains("hire", StringComparison.OrdinalIgnoreCase) || c.AuthorRole.Contains("CEO", StringComparison.OrdinalIgnoreCase))
            {
                bucket = "LEAD";
                replyText = $"Thanks for sharing this, {c.AuthorName}. Happy to discuss what outcome you are looking to achieve.";
            }
            else if (c.Text.Contains("disagree", StringComparison.OrdinalIgnoreCase) || c.Text.Length > 60)
            {
                bucket = "SUBSTANCE";
                replyText = $"Thanks for the detailed perspective, {c.AuthorName}. Which part has mattered most in practice?";
            }
            else if (c.AuthorRole.Contains("VP", StringComparison.OrdinalIgnoreCase) || c.AuthorRole.Contains("Head", StringComparison.OrdinalIgnoreCase))
            {
                bucket = "PEER";
                replyText = $"Thanks for adding this, {c.AuthorName}. What are you seeing from your side?";
            }
            else if (c.Text.Contains("crypto", StringComparison.OrdinalIgnoreCase) || c.Text.Contains("bit.ly", StringComparison.OrdinalIgnoreCase))
            {
                bucket = "NOISE";
                replyText = null; // Ignored / suppressed
            }
            else
            {
                bucket = "SUPPORT";
                replyText = $"Thanks for sharing this, {c.AuthorName}.";
            }

            counts[bucket]++;
            replies.Add(new ReplyTriageItem(c.CommentId, bucket, replyText));
        }

        return new ReplyTriageResult(counts, replies);
    }

    public InboxTriageResult TriageInbox(InboxTriageRequest request, IHumanizerEngine humanizer)
    {
        var list = new List<TriagedInboxItem>();
        foreach (var m in request.Messages)
        {
            if (m.HasCalendarLink || m.MessageText.Contains("calendly.com", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new TriagedInboxItem(m.MessageId, "SPAM", "Automated sales sequence with calendar link", null));
            }
            else if (m.MessageText.Contains("consult", StringComparison.OrdinalIgnoreCase) || m.MessageText.Contains("case study", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new TriagedInboxItem(m.MessageId, "LEAD", null, $"Hi {m.SenderName}, thanks for reaching out. What outcome are you working toward?"));
            }
            else
            {
                list.Add(new TriagedInboxItem(m.MessageId, "PEER", null, $"Thanks for reaching out, {m.SenderName}."));
            }
        }
        return new InboxTriageResult(list);
    }

    public AuditResult AuditContent(AuditRequest request)
    {
        // Calculate engagement rate: (reactions + comments + reposts) / impressions * 100
        var ranked = request.Posts.OrderByDescending(p => (double)(p.Reactions + p.Comments + p.Reposts) / Math.Max(p.Impressions, 1)).ToList();
        var top = ranked.Take(1).ToList();
        var bottom = ranked.Skip(1).Take(1).ToList();

        var stop = new List<string> { "Stop generic tool listicles without verified metrics." };
        var doMore = new List<string> { "Do more posts breaking down costly mistakes with concrete numbers." };

        return new AuditResult(top, bottom, stop, doMore);
    }

    public RepurposeResult ExtractAndRepurpose(RepurposeRequest request, IHumanizerEngine humanizer)
    {
        bool hasNumbers = Regex.IsMatch(request.SourceContent, @"\$\d|\d+%");
        bool hasMistakes = request.SourceContent.Contains("mistake", StringComparison.OrdinalIgnoreCase);
        var excerpts = Regex.Split(request.SourceContent.Trim(), @"(?<=[.!?])\s+|\r?\n")
            .Select(text => text.Trim())
            .Where(text => text.Length >= 24)
            .Distinct(StringComparer.Ordinal)
            .Take(4)
            .ToList();
        bool isThin = request.SourceContent.Length < 100 || (!hasNumbers && !hasMistakes) || excerpts.Count < 4;

        var counts = new Dictionary<string, int>
        {
            { "Claims", excerpts.Count },
            { "Numbers", hasNumbers ? 2 : 0 },
            { "Mistakes", hasMistakes ? 2 : 0 }
        };

        var posts = isThin
            ? new List<string>()
            : excerpts.Select((excerpt, index) => $"Post {index + 1} Â· {request.SourceTitle.Trim()}\n{excerpt}").ToList();

        return new RepurposeResult(isThin, counts, posts);
    }

    public ProfileAuditResult AuditAndRewriteProfile(ProfileAuditRequest request, IRubricEvaluator rubricEvaluator, IHumanizerEngine humanizer)
    {
        var profile = new LinkedInProfile(
            MemberId: "mem-audit",
            FullName: "Test User",
            Headline: request.Headline,
            About: request.About,
            BannerUrl: request.HasBanner ? "https://cdn.acme.com/banner.png" : null,
            PhotoUrl: "https://static.licdn.com/avatar.png",
            CustomUrl: null,
            ContactEmail: null,
            Experiences: request.ExperienceDescriptions.Select((d, idx) => new ProfileExperience($"exp-{idx}", "Role", "Company", "2020-2024", d, true)).ToList(),
            Educations: Array.Empty<ProfileEducation>(),
            Skills: Array.Empty<ProfileSkill>(),
            FeaturedItems: Array.Empty<ProfileFeaturedItem>(),
            RecommendationsCount: 0,
            LastPostDate: null,
            HasCreatorMode: false
        );

        var eval = rubricEvaluator.EvaluateProfile(profile);
        var rewrites = new Dictionary<string, string>
        {
            { "Headline", $"{request.Headline.Trim()} | [verified outcome you can support] | [audience or specialty]" },
            { "About", "Start with the audience or problem you work on.\n\nDescribe your approach using examples you can verify.\n\nAdd one to three results with their scope and timeframe only when you can substantiate them.\n\nClose with a next step that fits your goals." }
        };

        return new ProfileAuditResult(eval.TotalScore, eval.FixFirstCategories.Select(c => c.CategoryName).ToList(), rewrites);
    }

    public PostValidationResult ValidatePostConstraints(string postText)
    {
        var warnings = new List<string>();

        if (OutboundLinkRegex.IsMatch(postText))
        {
            warnings.Add("Contains outbound link in post body (algorithm penalty)");
        }

        var hashtags = HashtagRegex.Matches(postText);
        if (hashtags.Count > 3)
        {
            warnings.Add($"Found {hashtags.Count} hashtags. Maximum 3 hashtags recommended.");
        }

        if (PlaceholderRegex.IsMatch(postText))
        {
            warnings.Add("Contains unpopulated metric placeholder (e.g. {{your number}})");
        }

        return new PostValidationResult(warnings.Count > 0, warnings);
    }
}
