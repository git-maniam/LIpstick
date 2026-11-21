using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Infrastructure.LinkedIn;

public class MockLinkedInClient : ILinkedInClient
{
    public LinkedInConnectionState CurrentState { get; set; } = LinkedInConnectionState.Connected;
    public bool SimulateRateLimit { get; set; } = false;
    public bool SimulateServerError { get; set; } = false;
    public bool SimulateTokenExpired { get; set; } = false;
    public int SimulatedLatencyMs { get; set; } = 10;

    public LinkedInProfile? CustomProfile { get; set; }

    public async Task<LinkedInSessionInfo> GetSessionInfoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(SimulatedLatencyMs, cancellationToken);

        if (SimulateTokenExpired)
        {
            return new LinkedInSessionInfo(
                LinkedInConnectionState.TokenExpiring,
                "mem-12345",
                "Alex Vance",
                "Senior Product Manager at Acme Corp",
                "https://static.licdn.com/avatar.png",
                DateTime.UtcNow.AddMinutes(5),
                "Access token is expiring soon"
            );
        }

        if (CurrentState != LinkedInConnectionState.Connected)
        {
            return new LinkedInSessionInfo(
                LinkedInConnectionState.Disconnected,
                null, null, null, null, null,
                "Disconnected"
            );
        }

        return new LinkedInSessionInfo(
            LinkedInConnectionState.Connected,
            "mem-12345",
            "Alex Vance",
            "Senior Product Manager at Acme Corp",
            "https://static.licdn.com/avatar.png",
            DateTime.UtcNow.AddDays(30),
            "Connected"
        );
    }

    public async Task<LinkedInProfile> IngestProfileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (SimulateRateLimit)
            throw new HttpRequestException("429 Too Many Requests: Rate limit exceeded", null, System.Net.HttpStatusCode.TooManyRequests);

        if (SimulateServerError)
            throw new HttpRequestException("500 Internal Server Error", null, System.Net.HttpStatusCode.InternalServerError);

        await Task.Delay(SimulatedLatencyMs, cancellationToken);

        if (CustomProfile != null)
            return CustomProfile;

        // Default fixture profile (representative baseline with real-world flaws)
        return CreateDefaultFixtureProfile();
    }

    public async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(SimulatedLatencyMs, cancellationToken);
        if (SimulateServerError) return false;

        CurrentState = LinkedInConnectionState.Connected;
        SimulateTokenExpired = false;
        return true;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        CurrentState = LinkedInConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public static LinkedInProfile CreateDefaultFixtureProfile()
    {
        return new LinkedInProfile(
            MemberId: "mem-12345",
            FullName: "Alex Vance",
            Headline: "Senior Product Manager at Acme Corp | Passionate about building enterprise tech",
            About: "Passionate product leader with 8+ years experience leading teams. Helping companies leverage modern software solutions to scale operations. Reach out to discuss tech opportunities.",
            BannerUrl: null, // missing banner
            PhotoUrl: "https://static.licdn.com/avatar.png",
            CustomUrl: "https://linkedin.com/in/alex-vance-83b91a2", // uncustomized URL with digits
            ContactEmail: "alex@example.com",
            Experiences: new List<ProfileExperience>
            {
                new("exp-1", "Senior Product Manager", "Acme Corp", "2022 - Present", "Responsible for core platform features and roadmap prioritization. Manage sprint backlogs and coordinate cross-functional teams.", true),
                new("exp-2", "Product Manager", "Beta Systems", "2018 - 2022", "Handled feature requests and user research. Worked closely with engineering.", false)
            },
            Educations: new List<ProfileEducation>
            {
                new("edu-1", "State University", "B.S.", "Computer Science", "2014 - 2018")
            },
            Skills: new List<ProfileSkill>
            {
                new("Product Management", 14, true),
                new("Agile Methodologies", 8, true),
                new("Roadmapping", 3, false)
            },
            FeaturedItems: new List<ProfileFeaturedItem>
            {
                new("feat-1", "Quarterly Platform Review", "Slides from keynote", "https://example.com/slides")
            },
            RecommendationsCount: 1,
            LastPostDate: DateTime.UtcNow.AddDays(18),
            HasCreatorMode: false
        );
    }

    public static LinkedInProfile CreatePerfectFixtureProfile()
    {
        return new LinkedInProfile(
            MemberId: "mem-99999",
            FullName: "Sarah Connor",
            Headline: "Building AI agents that save B2B teams 10h/wk | $4.2M pipeline generated | Head of Ops @ Acme",
            About: "Most operations teams lose 15 hours a week wrestling fragmented data.\nI design automated workflows that eliminate that friction completely.\n\nOver the past 5 years:\n- Automated 60+ workflows saving $1.8M annually\n- Scaled pipelines to 10M daily events with 99.99% reliability\n- Led 12 engineers across 3 time zones\n\nSend a DM or email sarah@acme.com to discuss systems architecture.",
            BannerUrl: "https://cdn.acme.com/banner-custom.png",
            PhotoUrl: "https://cdn.acme.com/headshot.jpg",
            CustomUrl: "https://linkedin.com/in/sarahconnor",
            ContactEmail: "sarah@acme.com",
            Experiences: new List<ProfileExperience>
            {
                new("exp-1", "Head of Operations", "Acme Corp", "2021 - Present", "Saved $1.4M annually by automating logistics pipelines across 4 facilities. Reclaimed 12,000 engineering hours.", true),
                new("exp-2", "Senior Systems Engineer", "Beta Corp", "2017 - 2021", "Scaled infrastructure from 100k to 5M daily users. Cut cloud spend by 28% while improving SLA to 99.99%.", false)
            },
            Educations: new List<ProfileEducation>
            {
                new("edu-1", "MIT", "M.S.", "Systems Engineering", "2015 - 2017")
            },
            Skills: new List<ProfileSkill>
            {
                new("Distributed Systems", 42, true),
                new("Process Automation", 38, true),
                new("Operations Strategy", 29, true),
                new("Cloud Architecture", 25, false),
                new("Data Engineering", 20, false)
            },
            FeaturedItems: new List<ProfileFeaturedItem>
            {
                new("feat-1", "How we cut data latency by 90%", "Case study breakdown", "https://example.com/case-study"),
                new("feat-2", "Enterprise Automation Checklist", "Free architecture template", "https://example.com/template"),
                new("feat-3", "Book a 15-minute systems teardown", "Direct calendar link", "https://calendly.com/sarah")
            },
            RecommendationsCount: 5,
            LastPostDate: DateTime.UtcNow.AddDays(2),
            HasCreatorMode: true
        );
    }
}
