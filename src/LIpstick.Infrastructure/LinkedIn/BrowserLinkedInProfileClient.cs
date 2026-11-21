using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Infrastructure.LinkedIn;

/// <summary>
/// Opens LinkedIn in the user's default browser and fetches one saved public profile URL per scan.
/// Requests are unauthenticated: browser cookies and credentials are never accessed or sent.
/// </summary>
public sealed class BrowserLinkedInProfileClient : ILinkedInClient
{
    private static readonly HttpClient DefaultPublicClient = CreatePublicClient();
    private readonly ILocalDataService _dataService;
    private readonly HttpClient _publicClient;

    public BrowserLinkedInProfileClient(ILocalDataService dataService, HttpClient? publicClient = null)
    {
        _dataService = dataService;
        _publicClient = publicClient ?? DefaultPublicClient;
    }

    public async Task<LinkedInSessionInfo> ConnectAsync(CancellationToken cancellationToken = default)
    {
        Process.Start(new ProcessStartInfo("https://www.linkedin.com/login") { UseShellExecute = true });
        var config = await _dataService.LoadConfigAsync();
        return new LinkedInSessionInfo(
            string.IsNullOrWhiteSpace(config.LinkedInProfileUrl) ? LinkedInConnectionState.Disconnected : LinkedInConnectionState.Connected,
            null, null, null, null, null,
            "LinkedIn opened in your default browser. Your browser manages sign-in; LIpstick never receives or stores your password.");
    }

    public async Task<LinkedInSessionInfo> GetSessionInfoAsync(CancellationToken cancellationToken = default)
    {
        var config = await _dataService.LoadConfigAsync();
        bool configured = IsProfileUrl(config.LinkedInProfileUrl);
        string? name = FirstProfileLine(config.LinkedInProfileText);
        return new LinkedInSessionInfo(
            configured ? LinkedInConnectionState.Connected : LinkedInConnectionState.Disconnected,
            null, name, null, null, null,
            configured
                ? string.IsNullOrWhiteSpace(config.LinkedInProfileText) ? "Profile URL saved. Scan will try the public page; paste profile text if LinkedIn blocks it." : "Profile URL and fallback snapshot are saved. Scan will try to refresh from the public page."
                : "Open LinkedIn in your browser, then save your profile URL here.");
    }

    public async Task<LinkedInProfile> IngestProfileAsync(CancellationToken cancellationToken = default)
    {
        var config = await _dataService.LoadConfigAsync();
        if (!IsProfileUrl(config.LinkedInProfileUrl))
            throw new InvalidOperationException("Save a valid LinkedIn profile URL in Connections first.");
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var requestUri = new Uri(config.LinkedInProfileUrl!);
            HttpResponseMessage? response = null;
            for (int redirect = 0; redirect <= 3; redirect++)
            {
                response = await _publicClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!IsRedirect(response.StatusCode)) break;
                var location = response.Headers.Location ?? throw new InvalidDataException("LinkedIn returned a redirect without a destination.");
                var nextUri = location.IsAbsoluteUri ? location : new Uri(requestUri, location);
                response.Dispose();
                if (nextUri.Scheme != Uri.UriSchemeHttps || !IsLinkedInHost(nextUri.Host))
                    throw new InvalidDataException("LinkedIn redirected the profile request outside linkedin.com; the redirect was stopped.");
                requestUri = nextUri;
                response = null;
            }
            using var finalResponse = response ?? throw new InvalidDataException("LinkedIn returned too many redirects.");
            response = null;
            var responseToRead = finalResponse;
            if (responseToRead.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                throw new HttpRequestException($"LinkedIn did not allow an unauthenticated public profile request ({(int)responseToRead.StatusCode}).");
            responseToRead.EnsureSuccessStatusCode();

            var html = await responseToRead.Content.ReadAsStringAsync(cancellationToken);
            if (responseToRead.RequestMessage?.RequestUri is { } finalUri &&
                (!(finalUri.Host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase) || finalUri.Host.EndsWith(".linkedin.com", StringComparison.OrdinalIgnoreCase)) ||
                 finalUri.AbsolutePath.Contains("authwall", StringComparison.OrdinalIgnoreCase) ||
                 finalUri.AbsolutePath.Contains("login", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("LinkedIn redirected the public profile request to a sign-in page.");

            var profile = ParsePublicPage(config.LinkedInProfileUrl!, html);
            var updated = config with { LinkedInProfileText = profile.SourceSnapshotText };
            await _dataService.SaveConfigAsync(updated);
            return profile;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (!string.IsNullOrWhiteSpace(config.LinkedInProfileText))
        {
            return ParseProfile(config.LinkedInProfileUrl!, config.LinkedInProfileText!) with
            {
                SourceStatus = $"LinkedIn's public page could not be fetched ({ex.Message}). Scanned the last saved profile snapshot; paste a fresh copy to update it."
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not read this public LinkedIn page. It may require sign-in or block direct requests. Copy the visible profile sections into Scanner and scan again.", ex);
        }
    }

    public Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var config = await _dataService.LoadConfigAsync();
        await _dataService.SaveConfigAsync(config with { LinkedInProfileUrl = null, LinkedInProfileText = null });
    }

    public static bool IsProfileUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("www.linkedin.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.StartsWith("/in/", StringComparison.OrdinalIgnoreCase);

    private static HttpClient CreatePublicClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LIpstick/1.0 (public profile scan)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        return client;
    }

    private static bool IsLinkedInHost(string host) =>
        host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".linkedin.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    public static LinkedInProfile ParsePublicPage(string profileUrl, string html)
    {
        string visibleText = ExtractVisibleText(html);
        string? title = ExtractMeta(html, "og:title") ?? ExtractMeta(html, "twitter:title");
        string? description = ExtractMeta(html, "og:description") ?? ExtractMeta(html, "description") ?? ExtractMeta(html, "twitter:description");
        string normalizedTitle = WebUtility.HtmlDecode(title ?? string.Empty).Trim();
        normalizedTitle = Regex.Replace(normalizedTitle, @"\s*[|–-]\s*LinkedIn\s*$", string.Empty, RegexOptions.IgnoreCase);
        string? name = normalizedTitle.Split(" - ", 2, StringSplitOptions.TrimEntries).FirstOrDefault();

        LinkedInProfile profile;
        var candidateText = visibleText;
        if (string.IsNullOrWhiteSpace(candidateText) && !string.IsNullOrWhiteSpace(name))
            candidateText = $"{name}\n{description ?? string.Empty}";
        if (candidateText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length >= 2)
            profile = ParseProfile(profileUrl, candidateText);
        else
            throw new InvalidDataException("The public profile response did not contain readable profile content.");

        string headline = !string.IsNullOrWhiteSpace(description) ? WebUtility.HtmlDecode(description).Trim() : profile.Headline;
        if (headline.StartsWith("View ", StringComparison.OrdinalIgnoreCase) || headline.Length > 500)
            headline = profile.Headline;

        return profile with
        {
            FullName = string.IsNullOrWhiteSpace(name) ? profile.FullName : name,
            Headline = headline,
            About = string.IsNullOrWhiteSpace(profile.About) ? WebUtility.HtmlDecode(description ?? string.Empty).Trim() : profile.About,
            SourceSnapshotText = visibleText,
            SourceStatus = "Scanned the publicly accessible LinkedIn profile page without using browser credentials."
        };
    }

    private static string ExtractVisibleText(string html)
    {
        html = Regex.Replace(html, @"<head\b[^>]*>.*?</head>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var text = Regex.Replace(html, @"<(script|style|noscript|svg)\b[^>]*>.*?</\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<(nav|header|footer|aside)\b[^>]*>.*?</\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"</?(?:p|div|li|h[1-6]|section|article|br|tr|main|span)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[\t\x20]+", " ");
        text = Regex.Replace(text, @"\n\s*\n+", "\n");
        return text.Trim();
    }

    private static string? ExtractMeta(string html, string name)
    {
        string escaped = Regex.Escape(name);
        var tag = Regex.Match(html, $"<meta\\b(?=[^>]*\\b(?:property|name)\\s*=\\s*['\\\"]{escaped}['\\\"])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!tag.Success) return null;
        var content = Regex.Match(tag.Value, "\\bcontent\\s*=\\s*(['\\\"])(.*?)\\1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return content.Success ? WebUtility.HtmlDecode(content.Groups[2].Value) : null;
    }

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
