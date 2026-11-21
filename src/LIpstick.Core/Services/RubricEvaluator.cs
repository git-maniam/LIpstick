using System.Text.Json;
using System.Text.RegularExpressions;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Core.Services;

public class RubricEvaluator : IRubricEvaluator
{
    private static readonly Regex NumberRegex = new(@"\b\d[\d,.]*%?\b|\$\d", RegexOptions.Compiled);

    public RubricDefinition Definition { get; }

    public RubricEvaluator(string? rubricJsonContent = null)
    {
        Definition = LoadRubric(rubricJsonContent);
    }

    private static RubricDefinition LoadRubric(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var def = JsonSerializer.Deserialize<RubricDefinition>(json, options);
                if (def != null && def.Items.Count == 12 && def.Items.Sum(i => i.Points) == 100)
                {
                    return def;
                }
            }
            catch { }
        }

        // Authoritative built-in 12-category rubric (12/10/10/8/6/14/10/8/8/6/4/4 = 100 pts)
        var items = new List<RubricItem>
        {
            new("headline", "Headline", 12, "States who you help and what changes for them, carries one piece of proof, and is not just the job title. Uses the 220 characters, not 60."),
            new("about_open", "About: First 2 Lines", 10, "First two lines - everything visible before 'see more' - name the reader's problem. No 'passionate about', no third-person bio, no opening with your own name."),
            new("about_body", "About: Body & Proof", 10, "Written to one person in second person: their problem, what you do, one number as proof, what to do next. Under 1,400 characters."),
            new("featured", "Featured Items", 8, "Three items: best-performing post, a proof asset, and the way to contact you. Nothing empty, nothing stale."),
            new("banner", "Banner & Visual Positioning", 6, "One line of positioning and one way to reach you. Not the default gradient, not a stock skyline."),
            new("experience", "Experience Outcomes & Metrics", 14, "Last two roles detailed with clear scope and 2-3 outcome bullets with concrete numbers/metrics. Duties are not outcomes."),
            new("recommendations", "Recommendations", 10, "At least three received in the last two years, each naming a specific outcome rather than generic character praise."),
            new("skills", "Skills & Badges", 8, "Top three skills are the core three you want to be hired/known for and are endorsed. Clean, prioritized skill list."),
            new("creator_mode", "Creator Mode Cadence", 8, "Active posting or engagement within the last seven days. Regular publishing cadence."),
            new("education", "Education & Credentials", 6, "Relevant degrees, professional certifications, or recent continuous education with valid credentials."),
            new("custom_url", "Custom URL & Contact Clarity", 4, "Clean custom LinkedIn URL, working email or booking link in contact info, consistent handle."),
            new("photo", "Headshot & Aesthetic Consistency", 4, "Face fills roughly 60% of the frame, eyes visible, shot in the last two years, clear non-distracting background.")
        };

        return new RubricDefinition("1.0", 100, "Authoritative 12-category master rubric", items);
    }

    public RubricEvaluationResult EvaluateProfile(LinkedInProfile profile)
    {
        var categoryScores = new List<RubricCategoryScore>();

        foreach (var item in Definition.Items)
        {
            var score = EvaluateCategory(item, profile);
            categoryScores.Add(score);
        }

        int totalScore = categoryScores.Sum(c => c.PointsAwarded);
        int maxPossible = categoryScores.Sum(c => c.MaxPoints);

        // Sort by loss (MaxPoints - PointsAwarded) descending to determine Fix First
        var fixFirst = categoryScores
            .Where(c => c.PointsAwarded < c.MaxPoints)
            .OrderByDescending(c => c.MaxPoints - c.PointsAwarded)
            .Take(3)
            .ToList();

        // Update FixFirst flag on the top loss categories
        var finalizedScores = categoryScores.Select(c =>
            fixFirst.Any(f => f.CategoryId == c.CategoryId)
                ? c with { FixFirst = true }
                : c with { FixFirst = false }
        ).ToList();

        return new RubricEvaluationResult(
            TotalScore: Math.Min(totalScore, 100),
            MaxPossibleScore: maxPossible,
            Categories: finalizedScores,
            FixFirstCategories: fixFirst
        );
    }

    private static RubricCategoryScore EvaluateCategory(RubricItem item, LinkedInProfile profile)
    {
        int awarded = 0;
        var missing = new List<string>();
        string notes = "";

        switch (item.Id)
        {
            case "headline":
                // 12 pts: Length > 60 chars (4 pts), has numbers/proof (4 pts), mentions problem/solution or help (4 pts)
                if (string.IsNullOrWhiteSpace(profile.Headline))
                {
                    missing.Add("Headline is empty");
                    notes = "Add a compelling headline speaking directly to the audience.";
                }
                else
                {
                    if (profile.Headline.Length > 60) awarded += 4;
                    else missing.Add("Headline is too short (under 60 characters)");

                    if (NumberRegex.IsMatch(profile.Headline)) awarded += 4;
                    else missing.Add("No concrete numbers or verifiable proof in headline");

                    if (profile.Headline.Contains('|') || profile.Headline.Contains('-') || profile.Headline.Contains("helping", StringComparison.OrdinalIgnoreCase) || profile.Headline.Contains("building", StringComparison.OrdinalIgnoreCase) || profile.Headline.Contains("save", StringComparison.OrdinalIgnoreCase))
                        awarded += 4;
                    else missing.Add("Reads like a standard job title instead of a value proposition");

                    notes = $"{awarded}/12 pts. Headline length: {profile.Headline.Length} chars.";
                }
                break;

            case "about_open":
                // 10 pts: First two lines hook without 'passionate about' or third-person bio
                if (string.IsNullOrWhiteSpace(profile.About))
                {
                    missing.Add("About section is empty");
                    notes = "Write a strong two-line opening hook.";
                }
                else
                {
                    var lines = profile.About.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var firstLines = string.Join(" ", lines.Take(2));

                    if (firstLines.Contains("passionate about", StringComparison.OrdinalIgnoreCase) ||
                        firstLines.Contains("seeking", StringComparison.OrdinalIgnoreCase))
                    {
                        missing.Add("Avoid generic filler like 'passionate about' in the opening hook");
                        awarded += 3;
                    }
                    else if (firstLines.Length >= 40)
                    {
                        awarded += 10;
                    }
                    else
                    {
                        awarded += 5;
                        missing.Add("Opening lines are too brief to engage readers before 'see more'");
                    }
                    notes = $"Opening lines evaluation: {awarded}/10 pts.";
                }
                break;

            case "about_body":
                // 10 pts: Body has numbers/proof (5 pts), under 1400 chars (3 pts), has clear call to action (2 pts)
                if (string.IsNullOrWhiteSpace(profile.About))
                {
                    missing.Add("About body is missing");
                }
                else
                {
                    if (NumberRegex.IsMatch(profile.About)) awarded += 5;
                    else missing.Add("No metrics, stats, or numbers found in About summary");

                    if (profile.About.Length <= 1400 && profile.About.Length >= 200) awarded += 3;
                    else if (profile.About.Length > 1400) missing.Add("About section exceeds recommended 1400 characters");
                    else missing.Add("About section is too short (under 200 characters)");

                    if (profile.About.Contains("contact", StringComparison.OrdinalIgnoreCase) ||
                        profile.About.Contains("reach", StringComparison.OrdinalIgnoreCase) ||
                        profile.About.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                        profile.About.Contains("dm", StringComparison.OrdinalIgnoreCase) ||
                        profile.About.Contains("connect", StringComparison.OrdinalIgnoreCase))
                    {
                        awarded += 2;
                    }
                    else missing.Add("No clear call-to-action or contact next step");

                    notes = $"About body scored {awarded}/10 pts.";
                }
                break;

            case "featured":
                // 8 pts: 3 items (8 pts), 1-2 items (4 pts), 0 items (0 pts)
                if (profile.FeaturedItems == null || profile.FeaturedItems.Count == 0)
                {
                    missing.Add("Featured section has no pinned items");
                    notes = "Pin your best post, case study, or resource.";
                }
                else if (profile.FeaturedItems.Count >= 3)
                {
                    awarded = 8;
                    notes = "3+ pinned featured items present.";
                }
                else
                {
                    awarded = profile.FeaturedItems.Count * 3;
                    missing.Add("Fewer than 3 items pinned in Featured section");
                    notes = $"{profile.FeaturedItems.Count} item(s) found in Featured.";
                }
                break;

            case "banner":
                // 6 pts: Custom banner URL present
                if (!string.IsNullOrWhiteSpace(profile.BannerUrl) && !profile.BannerUrl.Contains("default", StringComparison.OrdinalIgnoreCase))
                {
                    awarded = 6;
                    notes = "Custom background banner detected.";
                }
                else
                {
                    missing.Add("Default or missing profile banner image");
                    notes = "Replace default gradient banner with customized positioning graphic.";
                }
                break;

            case "experience":
                // 14 pts: Roles detailed with outcome numbers
                if (profile.Experiences == null || profile.Experiences.Count == 0)
                {
                    missing.Add("No work experience entries recorded");
                }
                else
                {
                    int outcomeBulletRoles = profile.Experiences.Count(e => NumberRegex.IsMatch(e.Description ?? ""));
                    if (outcomeBulletRoles >= 2) awarded = 14;
                    else if (outcomeBulletRoles == 1)
                    {
                        awarded = 9;
                        missing.Add("Add measurable metrics ($ savings, % growth, headcount) to previous roles");
                    }
                    else
                    {
                        awarded = 5;
                        missing.Add("Experience descriptions list job duties instead of quantitative business outcomes");
                    }
                    notes = $"{profile.Experiences.Count} roles recorded, {outcomeBulletRoles} with measurable metrics.";
                }
                break;

            case "recommendations":
                // 10 pts: 3+ recs (10 pts), 1-2 recs (5 pts), 0 recs (0 pts)
                if (profile.RecommendationsCount >= 3)
                {
                    awarded = 10;
                    notes = $"{profile.RecommendationsCount} recommendations received.";
                }
                else if (profile.RecommendationsCount > 0)
                {
                    awarded = profile.RecommendationsCount * 3;
                    missing.Add("Fewer than 3 recommendations received in the last 2 years");
                    notes = $"{profile.RecommendationsCount} recommendation(s) received.";
                }
                else
                {
                    missing.Add("Zero recommendations on profile");
                    notes = "Request 2-3 specific outcome-based recommendations from colleagues/clients.";
                }
                break;

            case "skills":
                // 8 pts: Endorsed skills with top 3
                if (profile.Skills == null || profile.Skills.Count == 0)
                {
                    missing.Add("No skills added");
                }
                else
                {
                    var topSkills = profile.Skills.Where(s => s.IsTopSkill).ToList();
                    int endorsedCount = profile.Skills.Count(s => s.EndorsementCount > 0);

                    if (topSkills.Count >= 3 && endorsedCount >= 3) awarded = 8;
                    else if (profile.Skills.Count >= 5)
                    {
                        awarded = 5;
                        missing.Add("Pin your top 3 core skills and gather endorsements");
                    }
                    else
                    {
                        awarded = 3;
                        missing.Add("Fewer than 5 skills listed");
                    }
                    notes = $"{profile.Skills.Count} skills total, {endorsedCount} endorsed.";
                }
                break;

            case "creator_mode":
                // 8 pts: Post within 7 days (8 pts), within 30 days (4 pts), none (0 pts)
                if (profile.LastPostDate.HasValue)
                {
                    var daysSince = (DateTime.UtcNow - profile.LastPostDate.Value).TotalDays;
                    if (daysSince <= 7)
                    {
                        awarded = 8;
                        notes = $"Active posting cadence ({daysSince:F0} days ago).";
                    }
                    else if (daysSince <= 30)
                    {
                        awarded = 4;
                        missing.Add("Last post was over 7 days ago. Recommended cadence is 3-4x weekly.");
                        notes = $"Last post was {daysSince:F0} days ago.";
                    }
                    else
                    {
                        missing.Add("Activity feed inactive for more than 30 days");
                        notes = "Feed appears dormant.";
                    }
                }
                else
                {
                    missing.Add("No recent post activity recorded");
                    notes = "Feed has no activity.";
                }
                break;

            case "education":
                // 6 pts: Degrees/certifications
                if (profile.Educations != null && profile.Educations.Count > 0)
                {
                    awarded = 6;
                    notes = $"{profile.Educations.Count} education credential(s) listed.";
                }
                else
                {
                    missing.Add("No education entries or accredited certifications listed");
                    notes = "Add formal degree or relevant industry certifications.";
                }
                break;

            case "custom_url":
                // 4 pts: Custom URL without trailing random numbers
                if (!string.IsNullOrWhiteSpace(profile.CustomUrl) && !Regex.IsMatch(profile.CustomUrl, @"-[0-9a-f]{6,}"))
                {
                    awarded = 4;
                    notes = $"Clean custom profile URL: {profile.CustomUrl}";
                }
                else
                {
                    missing.Add("Default URL with random digits detected (e.g. /in/john-doe-49a812)");
                    notes = "Customize public profile URL handle in LinkedIn settings.";
                }
                break;

            case "photo":
                // 4 pts: Profile photo present
                if (!string.IsNullOrWhiteSpace(profile.PhotoUrl) && !profile.PhotoUrl.Contains("ghost", StringComparison.OrdinalIgnoreCase))
                {
                    awarded = 4;
                    notes = "High-resolution headshot profile photo present.";
                }
                else
                {
                    missing.Add("No custom profile photo uploaded (default avatar)");
                    notes = "Upload a professional high-resolution headshot.";
                }
                break;
        }

        return new RubricCategoryScore(
            CategoryId: item.Id,
            CategoryName: item.Name,
            PointsAwarded: Math.Min(awarded, item.Points),
            MaxPoints: item.Points,
            EvaluationNotes: notes,
            MissingSignals: missing,
            FixFirst: false
        );
    }
}
