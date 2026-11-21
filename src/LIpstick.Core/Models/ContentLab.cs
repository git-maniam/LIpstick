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
