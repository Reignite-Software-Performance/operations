namespace Operations.Issues;

/// <summary>
/// A tracked defect/incident, identified deterministically by its fingerprint.
/// </summary>
public sealed class Issue
{
    private int _occurrenceCount;
    private int _notificationStatus = (int)NewIssueNotificationStatus.Pending;

    /// <summary>Deterministic identifier derived from <see cref="Fingerprint"/>.</summary>
    public required string Id { get; init; }

    /// <summary>Stable fingerprint used to deduplicate incoming signatures.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>Human-readable signature of the underlying event.</summary>
    public required string Signature { get; set; }

    /// <summary>Current lifecycle state.</summary>
    public IssueState State { get; set; } = IssueState.New;

    /// <summary>How many times this fingerprint has been observed.</summary>
    public int OccurrenceCount
    {
        get => Volatile.Read(ref _occurrenceCount);
        set => Volatile.Write(ref _occurrenceCount, value);
    }

    /// <summary>UTC timestamp of the first observation (the onset).</summary>
    public DateTime FirstSeenUtc { get; set; }

    /// <summary>UTC timestamp of the most recent observation.</summary>
    public DateTime LastSeenUtc { get; set; }

    /// <summary>UTC timestamp at which the new-issue notification was delivered, if any.</summary>
    public DateTime? NewIssueNotifiedUtc { get; set; }

    /// <summary>Persisted delivery state of the new-issue notification.</summary>
    public NewIssueNotificationStatus NotificationStatus
    {
        get => (NewIssueNotificationStatus)Volatile.Read(ref _notificationStatus);
        set => Volatile.Write(ref _notificationStatus, (int)value);
    }

    /// <summary>True once the new-issue notification has been delivered.</summary>
    public bool IsNotificationDelivered =>
        Volatile.Read(ref _notificationStatus) == (int)NewIssueNotificationStatus.Delivered;

    /// <summary>Atomically records another occurrence of this issue's signature.</summary>
    public void RecordOccurrence(string signature, DateTime lastSeenUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);

        Signature = signature;
        LastSeenUtc = lastSeenUtc;
        Interlocked.Increment(ref _occurrenceCount);
    }

    /// <summary>
    /// Atomically claims the right to deliver the new-issue notification. Returns true only
    /// for the single caller that transitions the state from Pending to Delivering, which is
    /// what guarantees at most one notification even under concurrent ingestion.
    /// </summary>
    public bool TryClaimNotificationDelivery()
        => Interlocked.CompareExchange(
            ref _notificationStatus,
            (int)NewIssueNotificationStatus.Delivering,
            (int)NewIssueNotificationStatus.Pending) == (int)NewIssueNotificationStatus.Pending;

    /// <summary>Marks the notification as delivered.</summary>
    public void MarkNotificationDelivered()
        => Interlocked.Exchange(ref _notificationStatus, (int)NewIssueNotificationStatus.Delivered);

    /// <summary>Releases an in-progress claim so the notification can be retried.</summary>
    public void ReleaseNotificationClaim()
        => Interlocked.Exchange(ref _notificationStatus, (int)NewIssueNotificationStatus.Pending);

    /// <summary>
    /// Recovers a claim left in-progress by a crashed process so it becomes retryable again.
    /// </summary>
    public void RecoverStaleNotificationClaim()
        => Interlocked.CompareExchange(
            ref _notificationStatus,
            (int)NewIssueNotificationStatus.Pending,
            (int)NewIssueNotificationStatus.Delivering);
}
