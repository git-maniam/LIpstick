using LIpstick.Core.Models;

namespace LIpstick.Core.Interfaces;

public interface IAIHubService
{
    IReadOnlyList<IAIProvider> Providers { get; }
    Task<IReadOnlyList<ModelInfo>> GetAllAvailableModelsAsync(CancellationToken cancellationToken = default);
    IAIProvider ResolveProviderForModel(string modelId);
    Task<string> GenerateRecommendationAsync(RecommendationRequest request, CancellationToken cancellationToken = default);
}
