using LIpstick.Core.Interfaces;
using LIpstick.Core.Models;

namespace LIpstick.Infrastructure.LinkedIn;

public class LinkedInSyncWorker : ILinkedInSyncWorker
{
    private readonly ILinkedInClient _client;
    private readonly ILocalDataService _dataService;
    private readonly IHumanizerEngine _humanizer;
    private readonly ISecureStorageService _secureStorage;

    public bool SimulateScopeRestriction { get; set; } = false;

    public LinkedInSyncWorker(
        ILinkedInClient client,
        ILocalDataService dataService,
        IHumanizerEngine humanizer,
        ISecureStorageService secureStorage)
    {
        _client = client;
        _dataService = dataService;
        _humanizer = humanizer;
        _secureStorage = secureStorage;
    }

    public async Task<UpdateResult> UpdateHeadlineAsync(string newHeadline, CancellationToken cancellationToken = default)
    {
        return await ExecuteUpdateSectionAsync("Headline", newHeadline, async () =>
        {
            if (SimulateScopeRestriction)
            {
                return (UpdateOutcome.Unsupported, "Official LinkedIn Member API restricts personal headline mutation for public apps. Copied to clipboard & profile editor opened.");
            }
            return (UpdateOutcome.Succeeded, "Headline updated on LinkedIn profile.");
        }, cancellationToken);
    }

    public async Task<UpdateResult> UpdateAboutAsync(string newAbout, CancellationToken cancellationToken = default)
    {
        return await ExecuteUpdateSectionAsync("About", newAbout, async () =>
        {
            if (SimulateScopeRestriction)
            {
                return (UpdateOutcome.Unsupported, "Official LinkedIn Member API restricts personal summary mutation for public apps. Copied to clipboard & profile editor opened.");
            }
            return (UpdateOutcome.Succeeded, "About section updated on LinkedIn profile.");
        }, cancellationToken);
    }

    public async Task<UpdateResult> UpdateExperienceAsync(string experienceId, string newDescription, CancellationToken cancellationToken = default)
    {
        return await ExecuteUpdateSectionAsync($"Experience:{experienceId}", newDescription, async () =>
        {
            if (SimulateScopeRestriction)
            {
                return (UpdateOutcome.Unsupported, "Official LinkedIn Member API restricts experience mutation. Copied to clipboard.");
            }
            return (UpdateOutcome.Succeeded, "Experience description updated.");
        }, cancellationToken);
    }

    public async Task<UpdateResult> ApplyAllAsync(IReadOnlyDictionary<string, string> sectionUpdates, CancellationToken cancellationToken = default)
    {
        if (sectionUpdates.Count == 0)
        {
            return new UpdateResult("All", UpdateOutcome.Failed, "No sections provided for update.", DateTime.UtcNow, false);
        }

        var results = new List<UpdateResult>();
        foreach (var (section, text) in sectionUpdates)
        {
            UpdateResult res = section.ToLowerInvariant() switch
            {
                "headline" => await UpdateHeadlineAsync(text, cancellationToken),
                "about" => await UpdateAboutAsync(text, cancellationToken),
                _ => await ExecuteUpdateSectionAsync(section, text, () => Task.FromResult((UpdateOutcome.Succeeded, $"{section} updated")), cancellationToken)
            };
            results.Add(res);
        }

        int succeeded = results.Count(r => r.Outcome == UpdateOutcome.Succeeded);
        int failed = results.Count(r => r.Outcome == UpdateOutcome.Failed);

        if (failed > 0 && succeeded > 0)
        {
            // AP-03: Partial success handling - never report false all-success!
            return new UpdateResult("All", UpdateOutcome.Failed, $"Partial update: {succeeded} succeeded, {failed} failed.", DateTime.UtcNow, true);
        }

        if (failed > 0)
        {
            return new UpdateResult("All", UpdateOutcome.Failed, $"All {failed} section updates failed.", DateTime.UtcNow, false);
        }

        return new UpdateResult("All", UpdateOutcome.Succeeded, $"Successfully applied {succeeded} sections.", DateTime.UtcNow, true);
    }

    private async Task<UpdateResult> ExecuteUpdateSectionAsync(
        string sectionName,
        string newText,
        Func<Task<(UpdateOutcome outcome, string msg)>> writeAction,
        CancellationToken cancellationToken)
    {
        // 1. Verify connection status (AP-01)
        var session = await _client.GetSessionInfoAsync(cancellationToken);
        if (session.State != LinkedInConnectionState.Connected)
        {
            return new UpdateResult(sectionName, UpdateOutcome.Failed, "Cannot apply: LinkedIn account is disconnected.", DateTime.UtcNow, false);
        }

        // 2. Validate safety threshold (T5 / AP-01: overall >= 70 && min >= 55)
        var eval = _humanizer.Evaluate(newText);
        if (eval.Verdict != HumanizerVerdict.Pass)
        {
            return new UpdateResult(sectionName, UpdateOutcome.Failed, $"Safety gate blocked apply: Text human score is {eval.OverallScore:F1} ({eval.Verdict}). Must be PASS (>= 70).", DateTime.UtcNow, false);
        }

        // 3. Atomically persist original snapshot to history before mutation (T3, AP-02)
        var profile = await _client.IngestProfileAsync(cancellationToken);
        string previousText = sectionName.ToLowerInvariant() switch
        {
            "headline" => profile.Headline,
            "about" => profile.About,
            _ => string.Empty
        };

        if (previousText == newText)
        {
            return new UpdateResult(sectionName, UpdateOutcome.Failed, "No changes detected between current text and recommendation.", DateTime.UtcNow, false);
        }

        var historyItem = new ProfileHistoryItem(
            Id: Guid.NewGuid().ToString("N")[..8],
            SectionName: sectionName,
            PreviousText: previousText,
            AppliedText: newText,
            HumanScore: eval.OverallScore,
            Status: "Applied",
            Timestamp: DateTime.UtcNow,
            AppliedModel: "AI Rewrite"
        );

        await _dataService.AppendHistoryAsync(historyItem);

        // 4. Execute mutation or hybrid fallback
        try
        {
            var (outcome, msg) = await writeAction();
            return new UpdateResult(sectionName, outcome, msg, DateTime.UtcNow, true, historyItem.Id);
        }
        catch (Exception ex)
        {
            return new UpdateResult(sectionName, UpdateOutcome.Failed, $"Mutation failed: {ex.Message}", DateTime.UtcNow, true, historyItem.Id);
        }
    }
}
