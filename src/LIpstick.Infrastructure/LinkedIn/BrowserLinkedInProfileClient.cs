using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Infrastructure.LinkedIn;

/// <summary>
/// Uses the user's default browser for LinkedIn sign-in and scans a profile snapshot
/// explicitly supplied by the user. It never reads browser credentials/cookies or scrapes LinkedIn.
/// </summary>
public sealed class BrowserLinkedInProfileClient(ILocalDataService dataService) : ILinkedInClient
{
    public async Task<LinkedInSessionInfo> ConnectAsync(CancellationToken cancellationToken = default)
    {
        Process.Start(new ProcessStartInfo("https://www.linkedin.com/login") { UseShellExecute = true });
        var config = await dataService.LoadConfigAsync();
        return new LinkedInSessionInfo(
            string.IsNullOrWhiteSpace(config.LinkedInProfileUrl) ? LinkedInConnectionState.Disconnected : LinkedInConnectionState.Connected,
            null, null, null, null, null,
            "LinkedIn opened in your default browser. Your browser manages sign-in; LIpstick never receives or stores your password.");
    }

    public async Task<LinkedInSessionInfo> GetSessionInfoAsync(CancellationToken cancellationToken = default)
    {
        var config = await dataService.LoadConfigAsync();
        bool configured = IsProfileUrl(config.LinkedInProfileUrl);
        string? name = FirstProfileLine(config.LinkedInProfileText);
        return new LinkedInSessionInfo(
            configured ? LinkedInConnectionState.Connected : LinkedInConnectionState.Disconnected,
            null, name, null, null, null,
            configured
                ? string.IsNullOrWhiteSpace(config.LinkedInProfileText) ? "Profile URL saved. Paste your profile text in Scanner to create the first snapshot." : "Profile URL and local profile snapshot are ready to scan."
                : "Open LinkedIn in your browser, then save your profile URL here.");
    }

    public async Task<LinkedInProfile> IngestProfileAsync(CancellationToken cancellationToken = default)
    {
        var config = await dataService.LoadConfigAsync();
        if (!IsProfileUrl(config.LinkedInProfileUrl))
            throw new InvalidOperationException("Save a valid LinkedIn profile URL in Connections first.");
        if (string.IsNullOrWhiteSpace(config.LinkedInProfileText))
            throw new InvalidOperationException("Paste your LinkedIn profile text in Scanner and save the snapshot before scanning.");

        cancellationToken.ThrowIfCancellationRequested();
        return ParseProfile(config.LinkedInProfileUrl!, config.LinkedInProfileText!);
    }

    public Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var config = await dataService.LoadConfigAsync();
        await dataService.SaveConfigAsync(config with { LinkedInProfileUrl = null, LinkedInProfileText = null });
    }

    public static bool IsProfileUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("www.linkedin.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.StartsWith("/in/", StringComparison.OrdinalIgnoreCase);

    public static LinkedInProfile ParseProfile(string profileUrl, string profileText)
    {
        var lines = profileText.Replace("\r", string.Empty).Split('\n')
            .Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        if (lines.Length < 2)
            throw new InvalidDataException("The profile snapshot is too short. Copy the visible profile sections and try again.");

        var headings = new[] { "about", "experience", "education", "skills", "licenses & certifications", "featured", "recommendations", "interests", "contact" };
        int FindHeading(string heading) => Array.FindIndex(lines, line => line.Equals(heading, StringComparison.OrdinalIgnoreCase));
        string Section(string heading)
        {
            int start = FindHeading(heading);
            if (start < 0) return string.Empty;
            int end = lines.Length;
            foreach (var next in headings.Select(FindHeading).Where(index => index > start)) end = Math.Min(end, next);
            return string.Join(Environment.NewLine, lines.Skip(start + 1).Take(end - start - 1));
        }

        var name = lines[0];
        var headline = lines[1];
        var experienceText = Section("experience");
        var skillsText = Section("skills");
        var educationText = Section("education");
        var featuredText = Section("featured");
        var slug = new Uri(profileUrl).Segments.Last().Trim('/');
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slug)))[..16];
        var experiences = string.IsNullOrWhiteSpace(experienceText)
            ? Array.Empty<ProfileExperience>()
            : new[] { new ProfileExperience("snapshot-experience", "Profile experience", string.Empty, string.Empty, experienceText, false) };
        var educationLines = educationText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var educations = educationLines.Length == 0
            ? Array.Empty<ProfileEducation>()
            : new[] { new ProfileEducation("snapshot-education", educationLines[0], educationLines.ElementAtOrDefault(1) ?? string.Empty, string.Empty, string.Empty) };
        var featured = featuredText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((item, index) => new ProfileFeaturedItem($"snapshot-featured-{index + 1}", item, string.Empty, null)).ToArray();
        var skills = skillsText.Split([',', '\n', '·'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(skill => new ProfileSkill(skill, 0, false)).ToArray();

        var about = Section("about");
        if (string.IsNullOrWhiteSpace(about))
            about = string.Join(Environment.NewLine, lines.Skip(2));
        return new LinkedInProfile(id, name, headline, about, null, null, profileUrl, null,
            experiences, educations, skills, featured,
            Section("recommendations").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length, null, false)
        {
            SourceSnapshotText = profileText
        };
    }

    private static string? FirstProfileLine(string? text) =>
        text?.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
}
