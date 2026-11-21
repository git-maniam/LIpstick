using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Infrastructure.AI;

public class AIHubService : IAIHubService
{
    private readonly List<IAIProvider> _providers;

    public IReadOnlyList<IAIProvider> Providers => _providers.AsReadOnly();

    public AIHubService(IEnumerable<IAIProvider> providers)
    {
        _providers = providers?.ToList() ?? new List<IAIProvider>();
    }

    public AIHubService(GeminiProvider gemini, OpenAIProvider openAi, MockAIProvider mock)
        : this(new IAIProvider[] { gemini, openAi, mock })
    {
    }

    public async Task<IReadOnlyList<ModelInfo>> GetAllAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        var allModels = new List<ModelInfo>();

        foreach (var provider in _providers)
        {
            try
            {
                var models = await provider.ListAvailableModelsAsync(cancellationToken);
                if (models != null)
                {
                    allModels.AddRange(models);
                }
            }
            catch
            {
                // Unconfigured providers return empty without throwing in ListAvailableModelsAsync,
                // but protect against any individual provider errors.
            }
        }

        return allModels;
    }

    public IAIProvider ResolveProviderForModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return _providers.FirstOrDefault() ?? throw new InvalidOperationException("No AI providers configured in AIHubService.");
        }

        // 1. Direct name/prefix routing
        if (modelId.StartsWith("gemini", StringComparison.OrdinalIgnoreCase))
        {
            var gemini = _providers.FirstOrDefault(p => p.ProviderName.Contains("Gemini", StringComparison.OrdinalIgnoreCase));
            if (gemini != null) return gemini;
        }

        if (modelId.StartsWith("gpt", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o3", StringComparison.OrdinalIgnoreCase))
        {
            var openAi = _providers.FirstOrDefault(p => p.ProviderName.Contains("OpenAI", StringComparison.OrdinalIgnoreCase));
            if (openAi != null) return openAi;
        }

        // 2. Fallback to first provider
        return _providers.FirstOrDefault() ?? throw new InvalidOperationException("No AI providers configured in AIHubService.");
    }

    public async Task<string> GenerateRecommendationAsync(RecommendationRequest request, CancellationToken cancellationToken = default) =>
        (await GenerateCompletionAsync(request, cancellationToken)).Text;

    public async Task<AICompletionResult> GenerateCompletionAsync(RecommendationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var provider = ResolveProviderForModel(request.ModelName);
        return await provider.GenerateCompletionAsync(request, cancellationToken);
    }
}
