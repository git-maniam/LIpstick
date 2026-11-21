using LIpstick.Core.Models;

namespace LIpstick.Core.Interfaces;

public interface IAIProvider
{
    string ProviderName { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<ModelInfo>> ListAvailableModelsAsync(CancellationToken cancellationToken = default);
    Task<string> GenerateRecommendationAsync(RecommendationRequest request, CancellationToken cancellationToken = default);
}
