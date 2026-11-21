using LIpstick.Core.Models;

namespace LIpstick.Core.Interfaces;

public interface IAIProvider
{
    string ProviderName { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<ModelInfo>> ListAvailableModelsAsync(CancellationToken cancellationToken = default);
    Task<string> GenerateRecommendationAsync(RecommendationRequest request, CancellationToken cancellationToken = default);

    async Task<AICompletionResult> GenerateCompletionAsync(RecommendationRequest request, CancellationToken cancellationToken = default)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var text = await GenerateRecommendationAsync(request, cancellationToken);
        timer.Stop();
        return new AICompletionResult(text, request.ModelName, null, null, (int)timer.ElapsedMilliseconds);
    }
}
