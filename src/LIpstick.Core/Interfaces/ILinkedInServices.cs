using LIpstick.Core.Models;

namespace LIpstick.Core.Interfaces;

public interface ILinkedInClient
{
    Task<LinkedInSessionInfo> ConnectAsync(CancellationToken cancellationToken = default);
    Task<LinkedInSessionInfo> GetSessionInfoAsync(CancellationToken cancellationToken = default);
    Task<LinkedInProfile> IngestProfileAsync(CancellationToken cancellationToken = default);
    Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}

public interface ILinkedInSyncWorker
{
    Task<UpdateResult> UpdateHeadlineAsync(string newHeadline, CancellationToken cancellationToken = default);
    Task<UpdateResult> UpdateAboutAsync(string newAbout, CancellationToken cancellationToken = default);
    Task<UpdateResult> UpdateExperienceAsync(string experienceId, string newDescription, CancellationToken cancellationToken = default);
    Task<UpdateResult> ApplyAllAsync(IReadOnlyDictionary<string, string> sectionUpdates, CancellationToken cancellationToken = default);
}
