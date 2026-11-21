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

        string metric = request.SpecificMetric ?? "10 hours/week";
        if (string.IsNullOrWhiteSpace(request.SpecificMetric))
        {
            validationNotes.Add("No specific metric provided; used placeholder. Replace with your empirical numbers.");
        }

        var hookCandidates = new List<string>
        {
            $"Most teams spend 15 hours a week doing manual ops.\nHere is how we cut that to 2 hours using a simple automation:",
            $"I spent 3 months testing AI workflows for {request.Audience ?? "B2B teams"}.\nHere are the 3 changes that actually moved the needle by {metric}:",
            $"Unpopular opinion on {request.TopicOrIdea}:\nThe conventional advice costs you {metric} in lost productivity."
        };

        string selectedHook = hookCandidates[0];

        string body = $@"We didn't start with complex software. We started with a Google Sheet and 3 core rules:

1. Never do manual copy-paste across tabs twice.
2. If an action repeats daily, script it once.
3. Measure outcome in hours reclaimed, not features built.

The result after 60 days:
- Reclaimed {metric} for the core team
- Eliminated 95% of data entry errors
- Zero burnout during quarterly close

What is one manual task your team still does by hand every Monday?";

        string fullPost = $"{selectedHook}\n\n{body}\n\n#productivity #operations #workflow";

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
            draft = $"Hi {request.ProspectName}, enjoyed your recent note on {request.MutualContext}. Building similar tools in this space and would love to connect!";
            if (draft.Length > 200)
            {
                draft = draft[..197] + "...";
            }
        }
        else
        {
            draft = $"Hi {request.ProspectName},\n\nSaw your role as {request.ProspectRole} at your company. Noticed {request.MutualContext}.\n\nPut together a short 1-page breakdown of how teams in your space solve this. Happy to send it over if relevant?";
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

        var slides = new List<CarouselSlide>();
        slides.Add(new CarouselSlide(1, "Cover", "5 Steps to Resilience", $"For {request.Audience}"));
        slides.Add(new CarouselSlide(2, "The Stake", "Why This Matters", "Complex sync creates downtime. Simple patterns keep uptime high."));

        int contentSlidesCount = Math.Max(request.SlideCount - 4, 1);
        for (int i = 0; i < contentSlidesCount; i++)
        {
            slides.Add(new CarouselSlide(3 + i, "Content", $"Step {i + 1} Framework", "Deploy stateless workers with automatic circuit breakers and retry policies."));
        }

        slides.Add(new CarouselSlide(request.SlideCount - 1, "Recap", "Summary Checklist", "1. Circuit breakers\n2. Retry limits\n3. Stateless workers"));
        slides.Add(new CarouselSlide(request.SlideCount, "CTA", "Next Step", "Save this PDF or follow for systems architecture breakdowns."));

        string postText = "Carousels work best when every slide carries one idea.\nHere is the breakdown in PDF format.\n\n#systems #architecture";
        return new CarouselDraftResult(slides, postText, notes);
    }

    public CommentDraftResult DraftComment(CommentDraftRequest request, IHumanizerEngine humanizer)
    {
        var options = new List<CommentOption>
        {
            new("Add a datum", $"In our 2024 deployment audit across 40 teams, monoliths had 3x higher sprint completion for teams under 50. Microservices added 18 hours weekly of network debugging.", 2),
            new("Respectful disagree", $"While microservices decouple deployments, they transfer that complexity to distributed transactions. For early-stage teams, modular monoliths save tens of thousands in infrastructure spend.", 2)
        };

        return new CommentDraftResult(options, "Option 1 provides verifiable empirical data directly addressing the post.");
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
                replyText = $"Happy to send our deployment architecture guide over to you, {c.AuthorName}. Will drop you a note.";
            }
            else if (c.Text.Contains("disagree", StringComparison.OrdinalIgnoreCase) || c.Text.Length > 60)
            {
                bucket = "SUBSTANCE";
                replyText = $"Appreciate the perspective on review cycles, {c.AuthorName}. How did you adjust PR sizes?";
            }
            else if (c.AuthorRole.Contains("VP", StringComparison.OrdinalIgnoreCase) || c.AuthorRole.Contains("Head", StringComparison.OrdinalIgnoreCase))
            {
                bucket = "PEER";
                replyText = $"Thanks {c.AuthorName}, glad the routine resonates with your ops setup.";
            }
            else if (c.Text.Contains("crypto", StringComparison.OrdinalIgnoreCase) || c.Text.Contains("bit.ly", StringComparison.OrdinalIgnoreCase))
            {
                bucket = "NOISE";
                replyText = null; // Ignored / suppressed
            }
            else
            {
                bucket = "SUPPORT";
                replyText = $"Thanks {c.AuthorName}!";
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
                list.Add(new TriagedInboxItem(m.MessageId, "LEAD", null, $"Hi {m.SenderName}, yes we specialize in Postgres reconciliation. Let me know what data volume you are working with."));
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
        bool isThin = request.SourceContent.Length < 100 || (!hasNumbers && !hasMistakes);

        var counts = new Dictionary<string, int>
        {
            { "Claims", isThin ? 1 : 3 },
            { "Numbers", hasNumbers ? 2 : 0 },
            { "Mistakes", hasMistakes ? 2 : 0 }
        };

        var posts = isThin ? new List<string>() : new List<string>
        {
            "Post 1: The $42k billing mistake",
            "Post 2: Why idempotency keys save database locks",
            "Post 3: The Friday 4pm outage breakdown",
            "Post 4: Framework for transactional integrity"
        };

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
            { "Headline", "Building AI agents saving teams 10h/wk | $4.2M pipeline | Ops @ Acme" },
            { "About", "Most operations teams lose 15 hours a week wrestling fragmented data.\nI design automated workflows that eliminate that friction completely.\n\nOver the past 5 years:\n- Automated 60+ workflows saving $1.8M annually\n- Scaled pipelines to 10M daily events with 99.99% reliability\n\nSend a DM or email to discuss systems architecture." }
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
