using System.Text.Json;
using LIpstick.Core.Models;

namespace LIpstick.Infrastructure.LinkedIn;

public static class LinkedInProfileNormalizer
{
    public static LinkedInProfile NormalizeJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new ArgumentException("Profile JSON cannot be empty or whitespace.", nameof(rawJson));
        }

        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        // 1. MemberId
        string? memberId = GetStringProperty(root, "memberId", "id", "sub", "urn");
        if (string.IsNullOrWhiteSpace(memberId))
        {
            memberId = "unknown-member";
        }

        // 2. FullName
        string? fullName = GetStringProperty(root, "fullName", "name");
        if (string.IsNullOrWhiteSpace(fullName))
        {
            var firstName = GetStringProperty(root, "localizedFirstName", "firstName");
            var lastName = GetStringProperty(root, "localizedLastName", "lastName");
            fullName = $"{firstName} {lastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = "LinkedIn Member";
            }
        }

        // 3. Headline
        string headline = GetStringProperty(root, "headline", "localizedHeadline") ?? string.Empty;

        // 4. About
        string about = GetStringProperty(root, "about", "summary", "localizedSummary") ?? string.Empty;

        // 5. BannerUrl - empty/whitespace MUST normalize to null
        string? bannerUrl = NormalizeUrl(GetStringProperty(root, "bannerUrl", "backgroundPicture", "backgroundImage"));

        // 6. PhotoUrl - empty/whitespace MUST normalize to null
        string? photoUrl = NormalizeUrl(GetStringProperty(root, "photoUrl", "picture", "pictureUrl", "profilePicture"));

        // 7. CustomUrl / Vanity URL expansion ("alex-vance" -> "https://linkedin.com/in/alex-vance")
        string? rawCustomUrl = GetStringProperty(root, "customUrl", "vanityName", "publicProfileUrl");
        string? customUrl = NormalizeCustomUrl(rawCustomUrl);

        // 8. ContactEmail
        string? contactEmail = NormalizeStringOrNull(GetStringProperty(root, "contactEmail", "email", "emailAddress"));

        // 9. Experiences
        var experiences = ParseExperiences(root);

        // 10. Educations
        var educations = ParseEducations(root);

        // 11. Skills
        var skills = ParseSkills(root);

        // 12. FeaturedItems
        var featuredItems = ParseFeaturedItems(root);

        // 13. RecommendationsCount
        int recommendationsCount = GetIntProperty(root, "recommendationsCount", "numRecommendations");

        // 14. LastPostDate
        DateTime? lastPostDate = GetDateTimeProperty(root, "lastPostDate");

        // 15. HasCreatorMode
        bool hasCreatorMode = GetBoolProperty(root, "hasCreatorMode");

        return new LinkedInProfile(
            MemberId: memberId,
            FullName: fullName,
            Headline: headline,
            About: about,
            BannerUrl: bannerUrl,
            PhotoUrl: photoUrl,
            CustomUrl: customUrl,
            ContactEmail: contactEmail,
            Experiences: experiences,
            Educations: educations,
            Skills: skills,
            FeaturedItems: featuredItems,
            RecommendationsCount: recommendationsCount,
            LastPostDate: lastPostDate,
            HasCreatorMode: hasCreatorMode
        );
    }

    private static IReadOnlyList<ProfileExperience> ParseExperiences(JsonElement root)
    {
        if (!TryGetArrayProperty(root, out var expArray, "experiences", "positions", "elements"))
        {
            return Array.Empty<ProfileExperience>();
        }

        var list = new List<ProfileExperience>();
        int index = 1;
        foreach (var item in expArray.EnumerateArray())
        {
            string? id = GetStringProperty(item, "id", "entityUrn");
            if (string.IsNullOrWhiteSpace(id))
            {
                id = $"exp-{index}";
            }

            string title = GetStringProperty(item, "title", "role") ?? string.Empty;
            string company = GetStringProperty(item, "company", "companyName") ?? string.Empty;
            string dateRange = GetStringProperty(item, "dateRange", "timePeriod") ?? "2022 - Present";
            string description = GetStringProperty(item, "description", "summary") ?? string.Empty;
            bool isCurrent = GetBoolProperty(item, "isCurrent") || dateRange.Contains("Present", StringComparison.OrdinalIgnoreCase);

            list.Add(new ProfileExperience(id, title, company, dateRange, description, isCurrent));
            index++;
        }

        return list;
    }

    private static IReadOnlyList<ProfileEducation> ParseEducations(JsonElement root)
    {
        if (!TryGetArrayProperty(root, out var eduArray, "educations", "education"))
        {
            return Array.Empty<ProfileEducation>();
        }

        var list = new List<ProfileEducation>();
        int index = 1;
        foreach (var item in eduArray.EnumerateArray())
        {
            string? id = GetStringProperty(item, "id", "entityUrn");
            if (string.IsNullOrWhiteSpace(id))
            {
                id = $"edu-{index}";
            }

            string institution = GetStringProperty(item, "institution", "schoolName") ?? string.Empty;
            string degree = GetStringProperty(item, "degree", "degreeName") ?? string.Empty;
            string fieldOfStudy = GetStringProperty(item, "fieldOfStudy") ?? string.Empty;
            string years = GetStringProperty(item, "years", "timePeriod") ?? string.Empty;

            list.Add(new ProfileEducation(id, institution, degree, fieldOfStudy, years));
            index++;
        }

        return list;
    }

    private static IReadOnlyList<ProfileSkill> ParseSkills(JsonElement root)
    {
        if (!TryGetArrayProperty(root, out var skillArray, "skills"))
        {
            return Array.Empty<ProfileSkill>();
        }

        var list = new List<ProfileSkill>();
        foreach (var item in skillArray.EnumerateArray())
        {
            string name = GetStringProperty(item, "name", "skillName") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) && item.ValueKind == JsonValueKind.String)
            {
                name = item.GetString() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(name)) continue;

            int endorsementCount = GetIntProperty(item, "endorsementCount", "endorsements");
            bool isTopSkill = GetBoolProperty(item, "isTopSkill");

            list.Add(new ProfileSkill(name, endorsementCount, isTopSkill));
        }

        return list;
    }

    private static IReadOnlyList<ProfileFeaturedItem> ParseFeaturedItems(JsonElement root)
    {
        if (!TryGetArrayProperty(root, out var featArray, "featuredItems", "featured"))
        {
            return Array.Empty<ProfileFeaturedItem>();
        }

        var list = new List<ProfileFeaturedItem>();
        int index = 1;
        foreach (var item in featArray.EnumerateArray())
        {
            string? id = GetStringProperty(item, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                id = $"feat-{index}";
            }

            string title = GetStringProperty(item, "title") ?? string.Empty;
            string description = GetStringProperty(item, "description") ?? string.Empty;
            string? url = NormalizeUrl(GetStringProperty(item, "url"));

            list.Add(new ProfileFeaturedItem(id, title, description, url));
            index++;
        }

        return list;
    }

    private static string? NormalizeCustomUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }

        return $"https://linkedin.com/in/{raw.TrimStart('/')}";
    }

    private static string? NormalizeUrl(string? url)
    {
        return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
    }

    private static string? NormalizeStringOrNull(string? str)
    {
        return string.IsNullOrWhiteSpace(str) ? null : str.Trim();
    }

    private static string? GetStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private static int GetIntProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out int val))
                {
                    return val;
                }
            }
        }
        return 0;
    }

    private static bool GetBoolProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
            }
        }
        return false;
    }

    private static DateTime? GetDateTimeProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(prop.GetString(), out var dt))
                {
                    return dt.ToUniversalTime();
                }
            }
        }
        return null;
    }

    private static bool TryGetArrayProperty(JsonElement element, out JsonElement arrayElement, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                arrayElement = prop;
                return true;
            }
        }
        arrayElement = default;
        return false;
    }
}
