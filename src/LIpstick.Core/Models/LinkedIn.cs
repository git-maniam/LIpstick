namespace LIpstick.Core.Models;

public enum UpdateOutcome
{
    Succeeded,
    Failed,
    Unsupported,
    OutcomeUnknown
}

public record UpdateResult(
    string SectionName,
    UpdateOutcome Outcome,
    string? Message,
    DateTime Timestamp,
    bool RollbackSupported,
    string? SnapshotId = null
);

public enum LinkedInConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    TokenExpiring,
    Error
}

public record LinkedInSessionInfo(
    LinkedInConnectionState State,
    string? MemberId,
    string? FullName,
    string? Headline,
    string? AvatarUrl,
    DateTime? ExpiresAt,
    string? StatusMessage
);
