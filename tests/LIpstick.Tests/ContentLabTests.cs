using System.Text.RegularExpressions;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Core.Services;

namespace LIpstick.Tests;

[TestClass]
public class ContentLabTests
{
    private ContentLabService _service = null!;
    private HumanizerEngine _humanizer = null!;
    private RubricEvaluator _rubricEvaluator = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new ContentLabService();
        _humanizer = new HumanizerEngine();
        _rubricEvaluator = new RubricEvaluator();
    }

    [TestMethod]
    public void CL_01_All_11_Skill_Surfaces_Are_Available()
    {
        var skills = _service.GetSkillDefinitions();
        Assert.AreEqual(11, skills.Count, "Must have exactly 11 LinkedIn skills");

        var expectedSkills = new[]
        {
            "li-post", "li-carousel", "li-comment", "li-reply", "li-profile",
            "li-plan", "li-dm", "li-inbox", "li-audit", "li-repurpose", "li-human"
        };

        foreach (var id in expectedSkills)
        {
            Assert.IsTrue(skills.Any(s => s.Id == id), $"Skill {id} must be defined");
        }
    }

    [TestMethod]
    public void CL_02_Post_Drafting_Produces_Hooks_Body_And_Enforces_No_Links()
    {
        var req = new PostDraftRequest("Workflow Automation", "B2B leaders", "12 hours/week", "Hook-01");
        var result = _service.DraftPost(req, _humanizer);

        Assert.IsTrue(result.HookCandidates.Count >= 3, "Must offer 3 hook candidates");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.BodyDraft));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.FullCopyReadyPost));

        // Algorithmic check: no outbound link in the post body text
        Assert.IsFalse(result.FullCopyReadyPost.Contains("http://") || result.FullCopyReadyPost.Contains("https://"),
            "Outbound links must not appear in post body (algorithm penalty)");
    }

    [TestMethod]
    public void CL_02b_Post_Drafting_Rejects_Thin_Idea_Without_Specifics()
    {
        var req = new PostDraftRequest("post about AI", "general audience", null, null);
        var result = _service.DraftPost(req, _humanizer);

        Assert.IsTrue(result.ValidationNotes.Any(n => n.Contains("specific", StringComparison.OrdinalIgnoreCase) || n.Contains("metric", StringComparison.OrdinalIgnoreCase) || n.Contains("placeholder", StringComparison.OrdinalIgnoreCase)),
            "Thin idea must trigger validation note requesting empirical numbers or specifics");
        Assert.IsFalse(result.FullCopyReadyPost.Contains("$100M") || result.FullCopyReadyPost.Contains("10,000%"),
            "Must not hallucinate fabricated metrics");
    }

    [TestMethod]
    public void CL_02c_Post_Drafting_Enforces_Working_Length_And_Rejects_Engagement_Bait()
    {
        var req = new PostDraftRequest("Proposal automation cutting turnaround from 5h to 20m", "B2B leaders", "4h 40m saved", "Hook-17");
        var result = _service.DraftPost(req, _humanizer);

        Assert.IsTrue(result.FullCopyReadyPost.Length >= 400 && result.FullCopyReadyPost.Length <= 2000,
            $"Post length {result.FullCopyReadyPost.Length} must be in working range [400, 2000]");
        Assert.IsFalse(result.FullCopyReadyPost.Contains("Thoughts?"), "No engagement bait 'Thoughts?'");
        Assert.IsFalse(result.FullCopyReadyPost.Contains("Agree?"), "No engagement bait 'Agree?'");
        Assert.IsFalse(result.FullCopyReadyPost.Contains("Who else?"), "No engagement bait 'Who else?'");
    }

    [TestMethod]
    public void CL_03_Weekly_Plan_Follows_4x_Cadence_And_5_3_2_Rule()
    {
        var req = new WeeklyPlanRequest("EST", "B2B Operations", new[] { "Systems", "Automation" });
        var plan = _service.GenerateWeeklyPlan(req);

        Assert.AreEqual(4, plan.Schedule.Count, "Must produce 4x weekly posts");
        Assert.AreEqual(3, plan.EngagementRoutine.Count, "Must produce 5/3/2 engagement routine");

        var distinctTypes = plan.Schedule.Select(s => s.PostType).Distinct().Count();
        Assert.AreEqual(4, distinctTypes, "Post types must not repeat");
    }

    [TestMethod]
    public void CL_03b_Weekly_Plan_Assigns_Timezone_Aware_Peak_Morning_Slots_And_Four_Post_Mix()
    {
        var req = new WeeklyPlanRequest("PST", "Cloud Architecture", new[] { "Cost Optimization", "Reliability" });
        var plan = _service.GenerateWeeklyPlan(req);

        Assert.AreEqual(4, plan.Schedule.Count);
        foreach (var post in plan.Schedule)
        {
            Assert.IsTrue(post.LocalTimeSlot.Contains("AM"), "Schedule slots must target peak morning desk hours");
        }
        Assert.IsTrue(plan.StrategyNotes.Contains("PST"), "Strategy notes must reflect audience timezone");
        Assert.IsTrue(plan.EngagementRoutine.Any(r => r.Contains("5")), "Engagement routine includes 5 reach peers");
        Assert.IsTrue(plan.EngagementRoutine.Any(r => r.Contains("3")), "Engagement routine includes 3 peers");
        Assert.IsTrue(plan.EngagementRoutine.Any(r => r.Contains("2")), "Engagement routine includes 2 buyers");
    }

    [TestMethod]
    public void CL_04_Dm_Drafting_Enforces_200_Char_Limit_On_Invites()
    {
        var inviteReq = new DmDraftRequest("Jane Doe", "VP of Engineering", "your talk on distributed cache", true);
        var inviteResult = _service.DraftDm(inviteReq, _humanizer);

        Assert.IsTrue(inviteResult.WithinLimit);
        Assert.IsTrue(inviteResult.CharacterCount <= 200, $"Invite message length {inviteResult.CharacterCount} exceeds 200 chars");
    }

    [TestMethod]
    public void CL_04b_Dm_Drafting_MultiStep_FollowUp_Cadence_And_First_Message_Constraints()
    {
        var req = new DmDraftRequest("David Miller", "VP of Sales", "webinar on outbound pipeline", false);
        var result = _service.DraftDm(req, _humanizer);

        Assert.IsTrue(result.WithinLimit);
        Assert.IsTrue(result.CharacterCount <= 600, "First DM must be concise (< 600 chars)");
        Assert.IsFalse(result.DraftMessage.Contains("calendly.com") || result.DraftMessage.Contains("calendar"),
            "Message 1 must not pitch or push a calendar link");
        Assert.IsFalse(result.DraftMessage.Contains("I hope this message finds you well"),
            "No generic AI greetings");
    }

    [TestMethod]
    public void CL_04c_Carousel_Drafting_Enforces_8_To_12_Slides_Cover_Word_Count_And_Slide_Limits()
    {
        var req = new CarouselDraftRequest("5 Steps to Distributed Resilience", "Engineering Leaders", 9, "Simplicity beats complex sync");
        var result = _service.DraftCarousel(req, _humanizer);

        Assert.AreEqual(9, result.Slides.Count, "Must produce requested 9 slides");
        Assert.IsTrue(result.Slides[0].Headline.Split(' ').Length <= 6, "Slide 1 (Cover) headline must be 6 words or fewer");
        Assert.AreEqual("The Stake", result.Slides[1].SlideType, "Slide 2 must be The Stake");

        for (int i = 2; i < 7; i++)
        {
            int bodyWordCount = result.Slides[i].Body.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.IsTrue(bodyWordCount <= 25, $"Slide {i + 1} body words ({bodyWordCount}) must be <= 25");
        }

        Assert.AreEqual("Recap", result.Slides[7].SlideType, "Slide N+1 must be standalone Recap");
        Assert.AreEqual("CTA", result.Slides[8].SlideType, "Last slide must be single Call To Action");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.AccompanyingPostText), "Must generate 2-3 lines accompanying post text");
    }

    [TestMethod]
    public void CL_04d_Carousel_Rejection_Of_Under_8_And_Over_12_Slides()
    {
        var shortReq = new CarouselDraftRequest("Quick Tip", "Devs", 4, "Use logs");
        var longReq = new CarouselDraftRequest("Encyclopedia of Ops", "Devs", 16, "All 16 tips");

        var shortResult = _service.DraftCarousel(shortReq, _humanizer);
        Assert.IsTrue(shortResult.ValidationNotes.Any(n => n.Contains("text post", StringComparison.OrdinalIgnoreCase)),
            "Decks under 8 slides should recommend a text post");

        var longResult = _service.DraftCarousel(longReq, _humanizer);
        Assert.IsTrue(longResult.ValidationNotes.Any(n => n.Contains("12") || n.Contains("completion", StringComparison.OrdinalIgnoreCase)),
            "Decks over 12 slides must warn of completion rate drop");
    }

    [TestMethod]
    public void CL_04e_Comment_Drafting_Generates_Two_Archetypes_And_Bans_Generic_Openers()
    {
        var req = new CommentDraftRequest(
            "Microservices always reduce deployment risk and speed up delivery.",
            "Sarah Connor",
            "VP of Infrastructure",
            null,
            "Monoliths often have higher velocity for teams under 50"
        );

        var result = _service.DraftComment(req, _humanizer);
        Assert.AreEqual(2, result.Options.Count, "Must generate 2 options of different archetypes");
        Assert.AreNotEqual(result.Options[0].Archetype, result.Options[1].Archetype, "Archetypes must differ");

        foreach (var opt in result.Options)
        {
            Assert.IsFalse(opt.CommentText.StartsWith("Great post", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(opt.CommentText.StartsWith("Love this", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(opt.CommentText.StartsWith("So true", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(opt.CommentText.StartsWith("Sarah!", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(opt.CommentText.StartsWith("🔥") || opt.CommentText.StartsWith("👏"));
            Assert.IsTrue(opt.SentenceCount >= 2 && opt.SentenceCount <= 4, "Comment must be 2-4 sentences");
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.RecommendedOption), "Must give recommendation rationale");
    }

    [TestMethod]
    public void CL_04f_Reply_Triage_Categorizes_Five_Buckets_And_Suppresses_Noise()
    {
        var comments = new List<CommentItem>
        {
            new("c1", "Alice", "CEO @ Acme", "How do we hire your team to implement this pipeline?"),
            new("c2", "Bob", "Staff Eng", "I disagree on cadence: 4x a week burned out our team because review cycles stalled."),
            new("c3", "Carol", "VP Ops", "Spot on regarding the 5/3/2 routine."),
            new("c4", "Dan", "Dev", "Great post! 🔥"),
            new("c5", "SpamBot", "Sales Exec", "Check out our crypto agency at bit.ly/spam")
        };
        var req = new ReplyTriageRequest("Post on automation", comments);

        var result = _service.TriageAndDraftReplies(req, _humanizer);
        Assert.AreEqual(1, result.BucketCounts["LEAD"]);
        Assert.AreEqual(1, result.BucketCounts["SUBSTANCE"]);
        Assert.AreEqual(1, result.BucketCounts["PEER"]);
        Assert.AreEqual(1, result.BucketCounts["SUPPORT"]);
        Assert.AreEqual(1, result.BucketCounts["NOISE"]);

        var leadReply = result.Replies.First(r => r.CommentId == "c1");
        Assert.IsFalse(string.IsNullOrWhiteSpace(leadReply.DraftReplyText));
        Assert.IsTrue(leadReply.DraftReplyText.Contains("Happy to", StringComparison.OrdinalIgnoreCase) || leadReply.DraftReplyText.Contains("send", StringComparison.OrdinalIgnoreCase),
            "LEAD gets public answer with soft open door");

        var noiseReply = result.Replies.First(r => r.CommentId == "c5");
        Assert.IsNull(noiseReply.DraftReplyText, "NOISE/pitch gets zero reply / skipped to deny reach");
    }

    [TestMethod]
    public void CL_04g_Inbox_Triage_Detects_Bot_Sales_Sequences_As_Spam()
    {
        var messages = new List<InboxMessageItem>
        {
            new("m1", "John Tech", "CTO", "Saw your case study on data reconciliation. Do you consult on Postgres migrations?", DateTime.UtcNow, false, 0),
            new("m2", "GrowthBot", "BDR", "Quick question. I noticed you are in the software space. Book 15 min on my calendar: calendly.com/bot", DateTime.UtcNow, true, 1)
        };
        var req = new InboxTriageRequest(messages);

        var result = _service.TriageInbox(req, _humanizer);
        var validMsg = result.TriagedItems.First(i => i.MessageId == "m1");
        var botMsg = result.TriagedItems.First(i => i.MessageId == "m2");

        Assert.AreEqual("LEAD", validMsg.Bucket);
        Assert.IsNotNull(validMsg.DraftReply);

        Assert.AreEqual("SPAM", botMsg.Bucket);
        Assert.IsTrue(botMsg.TellDetected!.Contains("calendar", StringComparison.OrdinalIgnoreCase) || botMsg.TellDetected.Contains("sequence", StringComparison.OrdinalIgnoreCase));
        Assert.IsNull(botMsg.DraftReply, "SPAM messages must be archived with no draft reply");
    }

    [TestMethod]
    public void CL_04h_Content_Audit_Ranks_By_Engagement_Rate_And_Prescribes_Actionable_Pivots()
    {
        var posts = new List<PostAnalyticsItem>
        {
            new("p1", "Hook-05", "Text", "7 Tools for Founders", 10000, 30, 2, 1, 0, DayOfWeek.Tuesday),
            new("p2", "Hook-03", "Text", "How a bad contract cost us $18,000", 2000, 150, 45, 12, 15, DayOfWeek.Thursday)
        };
        var req = new AuditRequest(posts, 5000);

        var result = _service.AuditContent(req);
        Assert.AreEqual("p2", result.TopPerformers[0].PostId, "Ranked by engagement rate, post B must beat post A despite fewer impressions");
        Assert.AreEqual("p1", result.BottomPerformers[0].PostId);
        Assert.IsTrue(result.StopDoing.Any(s => s.Contains("tool", StringComparison.OrdinalIgnoreCase) || s.Contains("listicle", StringComparison.OrdinalIgnoreCase)),
            "Must recommend stopping low-engagement listicles");
        Assert.IsTrue(result.DoMore.Any(d => d.Contains("cost") || d.Contains("number") || d.Contains("mistake")),
            "Must recommend doing more posts with concrete costs/mistakes");
    }

    [TestMethod]
    public void CL_04i_Repurpose_Extracts_Standalone_Assets_And_Rejects_Thin_Content()
    {
        string richText = "We spent 3 months rewriting our billing engine. The mistake was keeping mongo. Cost was $42,000 in lost invoices. The mechanism: idempotency keys on every webhook. The story: on Friday at 4pm the queue froze. Quotable: Never let database transactions cross network boundaries.";
        var richReq = new RepurposeRequest("Billing Post-Mortem", richText, "Blog");
        var thinReq = new RepurposeRequest("Quick Note", "AI is great. Use AI today.", "Note");

        var richResult = _service.ExtractAndRepurpose(richReq, _humanizer);
        Assert.IsFalse(richResult.IsThinAsset);
        Assert.IsTrue(richResult.ExtractionCounts["Claims"] > 0);
        Assert.IsTrue(richResult.ExtractionCounts["Numbers"] > 0);
        Assert.IsTrue(richResult.ExtractionCounts["Mistakes"] > 0);
        Assert.AreEqual(4, richResult.ProposedWeekPosts.Count, "Proposes 4 distinct posts for the week");

        var thinResult = _service.ExtractAndRepurpose(thinReq, _humanizer);
        Assert.IsTrue(thinResult.IsThinAsset, "Asset yielding fewer than 4 extractable elements must be marked thin");
    }

    [TestMethod]
    public void CL_04j_Profile_Audit_And_Rewrite_Enforces_Section_Limits_And_Tone()
    {
        var req = new ProfileAuditRequest(
            Headline: "Senior Product Manager at Acme Corp",
            About: "Passionate product manager with 8 years experience leading teams...",
            ExperienceDescriptions: new[] { "Managed agile sprints." },
            HasBanner: false,
            FeaturedCount: 0
        );

        var result = _service.AuditAndRewriteProfile(req, _rubricEvaluator, _humanizer);
        Assert.IsTrue(result.CurrentScore < 60);
        Assert.IsTrue(result.FixFirstList.Count > 0);

        string rewrittenHeadline = result.ProposedRewrites["Headline"];
        Assert.IsTrue(rewrittenHeadline.Length <= 220, "Headline rewrite must not exceed 220 chars");
        Assert.IsTrue(rewrittenHeadline.Contains('|'), "Headline should follow formula with separators");

        string rewrittenAbout = result.ProposedRewrites["About"];
        Assert.IsFalse(rewrittenAbout.StartsWith("Passionate", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(rewrittenAbout.Length <= 1400, "About body rewrite must be under 1,400 chars");
    }

    [TestMethod]
    public void CL_05_Gate_T6_Content_Lab_Never_Mutates_LinkedIn_Or_Publishes_Automatically()
    {
        var ctorParams = typeof(ContentLabService).GetConstructors()[0].GetParameters();
        Assert.IsFalse(ctorParams.Any(p => p.ParameterType.Name.Contains("LinkedInSync") || p.ParameterType.Name.Contains("ILinkedInClient")),
            "Gate T6: ContentLabService must NEVER possess LinkedIn mutation authority");
    }

    [TestMethod]
    public void CL_06_Validator_Flags_Hashtag_Overflow_Outbound_Links_And_Fabricated_Metrics()
    {
        string postWithViolations = "Check out our tool at https://acme.com/demo #tech #startup #ai #growth #saas #scale\nWe achieved {{your number}} revenue.";

        var validation = _service.ValidatePostConstraints(postWithViolations);
        Assert.IsTrue(validation.HasWarnings);
        Assert.IsTrue(validation.Warnings.Any(w => w.Contains("outbound link", StringComparison.OrdinalIgnoreCase)),
            "Must flag outbound link in post body");
        Assert.IsTrue(validation.Warnings.Any(w => w.Contains("hashtag", StringComparison.OrdinalIgnoreCase)),
            "Must flag > 3 hashtags");
        Assert.IsTrue(validation.Warnings.Any(w => w.Contains("metric") || w.Contains("placeholder")),
            "Must flag unpopulated metric placeholder");
    }
}
