using System.Text.RegularExpressions;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Core.Services;

public interface IContentLabService
{
    IReadOnlyList<SkillToolDefinition> GetSkillDefinitions();
    PostDraftResult DraftPost(PostDraftRequest request, IHumanizerEngine humanizer);
    WeeklyPlanResult GenerateWeeklyPlan(WeeklyPlanRequest request);
    DmDraftResult DraftDm(DmDraftRequest request, IHumanizerEngine humanizer);
}

public class ContentLabService : IContentLabService
{
    private static readonly Regex OutboundLinkRegex = new(@"https?://\S+", RegexOptions.Compiled);
    private static readonly Regex HashtagRegex = new(@"#\w+", RegexOptions.Compiled);

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
}
