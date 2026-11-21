using LIpstick.Core.Models;

namespace LIpstick.Core.Interfaces;

public interface IContentLabService
{
    IReadOnlyList<SkillToolDefinition> GetSkillDefinitions();
    
    // Core Creation Skills
    PostDraftResult DraftPost(PostDraftRequest request, IHumanizerEngine humanizer);
    WeeklyPlanResult GenerateWeeklyPlan(WeeklyPlanRequest request);
    DmDraftResult DraftDm(DmDraftRequest request, IHumanizerEngine humanizer);
    CarouselDraftResult DraftCarousel(CarouselDraftRequest request, IHumanizerEngine humanizer);
    CommentDraftResult DraftComment(CommentDraftRequest request, IHumanizerEngine humanizer);
    
    // Engagement & Networking Triaging
    ReplyTriageResult TriageAndDraftReplies(ReplyTriageRequest request, IHumanizerEngine humanizer);
    InboxTriageResult TriageInbox(InboxTriageRequest request, IHumanizerEngine humanizer);
    
    // Analytics & Repurposing
    AuditResult AuditContent(AuditRequest request);
    RepurposeResult ExtractAndRepurpose(RepurposeRequest request, IHumanizerEngine humanizer);
    
    // Profile Polish
    ProfileAuditResult AuditAndRewriteProfile(ProfileAuditRequest request, IRubricEvaluator rubricEvaluator, IHumanizerEngine humanizer);
    
    // Safeguards & Gating
    PostValidationResult ValidatePostConstraints(string postText);
}
