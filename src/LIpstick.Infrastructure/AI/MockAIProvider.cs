using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;
using LIpstick.Core.Services;

namespace LIpstick.Infrastructure.AI;

public class MockAIProvider : IAIProvider
{
    public string ProviderName { get; }
    public bool IsConfigured { get; set; } = true;

    public bool SimulateQuotaExceeded { get; set; } = false;
    public bool SimulateInvalidKey { get; set; } = false;
    public bool SimulateTimeout { get; set; } = false;
    public bool SimulateRateLimit { get; set; } = false;
    public bool SimulateNetworkError { get; set; } = false;
    public int SimulatedLatencyMs { get; set; } = 10;
    public string? CustomRecommendationText { get; set; }

    public RecommendationRequest? LastRequest { get; private set; }
    public string? LastAssembledPrompt { get; private set; }

    public MockAIProvider(string providerName = "Google Gemini")
    {
        ProviderName = providerName;
    }

    public async Task<IReadOnlyList<ModelInfo>> ListAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (SimulateInvalidKey)
            throw new AIProviderException(ProviderName, "Invalid API key or unauthorized access", AIErrorCodes.Unauthorized);

        await Task.Delay(SimulatedLatencyMs, cancellationToken);

        if (ProviderName.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return new List<ModelInfo>
            {
                new("gemini-2.5-flash", "Gemini 2.5 Flash (Fast)", ProviderName, 1000000, 350, true),
                new("gemini-2.0-flash", "Gemini 2.0 Flash (Default)", ProviderName, 1000000, 420, true),
                new("gemini-1.5-pro", "Gemini 1.5 Pro (Deep Reasoning)", ProviderName, 2000000, 850, true)
            };
        }
        else
        {
            return new List<ModelInfo>
            {
                new("gpt-4o", "GPT-4o (Omni)", ProviderName, 128000, 450, true),
                new("gpt-4o-mini", "GPT-4o mini (Fast)", ProviderName, 128000, 280, true),
                new("o3-mini", "o3-mini (Reasoning)", ProviderName, 200000, 950, true)
            };
        }
    }

    public async Task<string> GenerateRecommendationAsync(RecommendationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LastRequest = request;
        LastAssembledPrompt = PromptContextAssembler.AssemblePrompt(request);

        if (SimulateInvalidKey)
            throw new AIProviderException(ProviderName, "API key is invalid or expired", AIErrorCodes.InvalidKey);

        if (SimulateQuotaExceeded)
            throw new AIProviderException(ProviderName, "Rate limit or quota exceeded for current tier", AIErrorCodes.QuotaExceeded);

        if (SimulateRateLimit)
            throw new AIProviderException(ProviderName, "Rate limit exceeded (429)", AIErrorCodes.RateLimitExceeded);

        if (SimulateNetworkError)
            throw new AIProviderException(ProviderName, "Network failure calling AI provider", AIErrorCodes.NetworkError);

        if (SimulateTimeout)
        {
            await Task.Delay(5000, cancellationToken);
            throw new TimeoutException("The AI provider request timed out.");
        }

        await Task.Delay(SimulatedLatencyMs, cancellationToken);

        if (!string.IsNullOrEmpty(CustomRecommendationText))
            return CustomRecommendationText;

        // Context-aware deterministic clean recommendation matching humanizer specs
        return request.SectionName.ToLowerInvariant() switch
        {
            "headline" => "Building AI agents that save B2B teams 10 hours a week | $4.2M pipeline generated | Head of Product @ Acme",
            "about" => "Most ops leaders lose 12 hours every week wrestling fragmented tools.\nI build deterministic automations that cut that manual grind to zero.\n\nOver the last 4 years at Acme and Beta Corp:\n- Automated 45 core business workflows across 6 departments\n- Reclaimed 14,000 engineering hours\n- Scaled data pipelines from 10k to 2.5M daily events with 99.98% uptime\n\nIf you want to streamline your team's workflow, send a DM or email me at contact@domain.com.",
            "experience" => "Led cross-functional team of 8 engineers delivering enterprise data sync:\n- Reduced sync latency from 45 minutes to 3.2 seconds across 12M records\n- Cut AWS infrastructure costs by $180,000 annually through query optimization\n- Onboarded 14 enterprise pilot customers with zero SLA breaches",
            _ => $"Proven outcomes for {request.SectionName}: Delivered 35% efficiency gains and saved $240,000 in operational overhead across 14 quarters."
        };
    }
}
