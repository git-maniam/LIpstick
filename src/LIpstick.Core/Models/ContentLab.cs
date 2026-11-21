namespace LIpstick.Core.Models;

public record SkillToolDefinition(
    string Id,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<string> RequiredInputs
);

public record PostDraftRequest(
    string TopicOrIdea,
    string Audience,
    string? SpecificMetric,
    string? DesiredHookId
);

public record PostDraftResult(
    IReadOnlyList<string> HookCandidates,
    string SelectedHook,
    string BodyDraft,
    string FullCopyReadyPost,
    HumanizerResult HumanizerScore,
    IReadOnlyList<string> ValidationNotes
);

public record WeeklyPlanRequest(
    string AudienceTimezone,
    string ProfessionalDomain,
    IReadOnlyList<string> TargetPillars
);

public record ScheduledPost(
    DayOfWeek Day,
    string PostType,
    string Angle,
    string HookFormulaId,
    string LocalTimeSlot,
    string TopicOutline
);

public record WeeklyPlanResult(
    IReadOnlyList<ScheduledPost> Schedule,
    IReadOnlyList<string> EngagementRoutine, // 5/3/2 list
    string StrategyNotes
);

public record DmDraftRequest(
    string ProspectName,
    string ProspectRole,
    string MutualContext,
    bool IsInviteNote // true = 200 char limit, false = follow-up
);

public record DmDraftResult(
    string DraftMessage,
    int CharacterCount,
    bool WithinLimit,
    HumanizerResult HumanizerScore
);

// Carousel & PDF Deck Models
public record CarouselDraftRequest(string Topic, string Audience, int SlideCount, string KeyTakeaway);
public record CarouselSlide(int SlideNumber, string SlideType, string Headline, string Body);
public record CarouselDraftResult(IReadOnlyList<CarouselSlide> Slides, string AccompanyingPostText, IReadOnlyList<string> ValidationNotes);

// Comment Drafting Models (9 Archetypes)
public record CommentDraftRequest(string PostText, string AuthorName, string AuthorRole, string? DesiredArchetype, string YourPerspective);
public record CommentOption(string Archetype, string CommentText, int SentenceCount);
public record CommentDraftResult(IReadOnlyList<CommentOption> Options, string RecommendedOption);

// Reply Triaging Models
public record CommentItem(string CommentId, string AuthorName, string AuthorRole, string Text);
public record ReplyTriageRequest(string PostTopic, IReadOnlyList<CommentItem> Comments);
public record ReplyTriageItem(string CommentId, string Bucket, string? DraftReplyText);
public record ReplyTriageResult(IReadOnlyDictionary<string, int> BucketCounts, IReadOnlyList<ReplyTriageItem> Replies);

// Inbox Triaging & Bot Filtering Models
public record InboxMessageItem(string MessageId, string SenderName, string SenderTitle, string MessageText, DateTime ReceivedUtc, bool HasCalendarLink, int FollowUpCount);
public record InboxTriageRequest(IReadOnlyList<InboxMessageItem> Messages);
public record TriagedInboxItem(string MessageId, string Bucket, string? TellDetected, string? DraftReply);
public record InboxTriageResult(IReadOnlyList<TriagedInboxItem> TriagedItems);

// Post-Mortem Analytics Audit Models
public record PostAnalyticsItem(string PostId, string HookId, string Format, string Topic, int Impressions, int Reactions, int Comments, int Reposts, int ProfileClicks, DayOfWeek Day);
public record AuditRequest(IReadOnlyList<PostAnalyticsItem> Posts, int FollowerCount);
public record AuditResult(IReadOnlyList<PostAnalyticsItem> TopPerformers, IReadOnlyList<PostAnalyticsItem> BottomPerformers, IReadOnlyList<string> StopDoing, IReadOnlyList<string> DoMore);

// Long-Form Repurposing Models
public record RepurposeRequest(string SourceTitle, string SourceContent, string SourceFormat);
public record RepurposeResult(bool IsThinAsset, IReadOnlyDictionary<string, int> ExtractionCounts, IReadOnlyList<string> ProposedWeekPosts);

// Profile Audit & Rewrite Models
public record ProfileAuditRequest(string Headline, string About, IReadOnlyList<string> ExperienceDescriptions, bool HasBanner, int FeaturedCount);
public record ProfileAuditResult(int CurrentScore, IReadOnlyList<string> FixFirstList, IReadOnlyDictionary<string, string> ProposedRewrites);

// Content Safeguards & Constraint Validation Models
public record PostValidationResult(bool HasWarnings, IReadOnlyList<string> Warnings);

