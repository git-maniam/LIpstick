using LIpstick.Core.Models;

namespace LIpstick.Core.Interfaces;

public interface IHumanizerEngine
{
    (string CleanedText, HumanizeReport Report) CleanText(string input);
    HumanizerResult Evaluate(string text, string? referenceVoice = null);
    Task<HumanizerResult> EnsurePassingAsync(
        string initialText,
        Func<string, CancellationToken, Task<string>> rewriteFunc,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default);
}
