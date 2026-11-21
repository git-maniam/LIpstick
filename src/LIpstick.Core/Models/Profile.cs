namespace LIpstick.Core.Models;

public record ProfileExperience(
    string Id,
    string Title,
    string Company,
    string DateRange,
    string Description,
    bool IsCurrent
);

public record ProfileEducation(
    string Id,
    string Institution,
    string Degree,
    string FieldOfStudy,
    string Years
);

public record ProfileFeaturedItem(
    string Id,
    string Title,
    string Description,
    string? Url
);

public record ProfileSkill(
    string Name,
    int EndorsementCount,
    bool IsTopSkill
);

public record LinkedInProfile(
    string MemberId,
    string FullName,
    string Headline,
    string About,
    string? BannerUrl,
    string? PhotoUrl,
    string? CustomUrl,
    string? ContactEmail,
    IReadOnlyList<ProfileExperience> Experiences,
    IReadOnlyList<ProfileEducation> Educations,
    IReadOnlyList<ProfileSkill> Skills,
    IReadOnlyList<ProfileFeaturedItem> FeaturedItems,
    int RecommendationsCount,
    DateTime? LastPostDate,
    bool HasCreatorMode
)
{
    /// <summary>Complete user-provided profile text for provenance and future processing.</summary>
    public string? SourceSnapshotText { get; init; }
    public string? SourceStatus { get; init; }
}
