namespace Operations.Ingestion;

/// <summary>
/// Accepts incoming signature events, deduplicates them by fingerprint and notifies the
/// group when a new issue is created.
/// </summary>
public interface IIssueIngestionService
{
    /// <summary>
    /// Ingests a signature. Atomically creates a new issue and fires exactly one new-issue
    /// notification when the fingerprint is unseen; otherwise increments the existing
    /// issue without sending another new-issue notification.
    /// </summary>
    Task<IngestionResult> IngestSignatureAsync(
        string signature,
        string fingerprint,
        DateTime occurredUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries all new-issue notifications whose delivery is still pending (including stale
    /// in-progress claims), providing at-least-once delivery for transient channel failures.
    /// Returns the number of notifications delivered by this call.
    /// </summary>
    Task<int> DeliverPendingNotificationsAsync(CancellationToken cancellationToken = default);
}
