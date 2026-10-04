using Microsoft.Extensions.Logging;
using Operations.Issues;
using Operations.Notifications;

namespace Operations.Ingestion;

/// <summary>
/// Fingerprint-based deduplication on ingestion. When a fingerprint is seen for the first
/// time the service atomically persists a new <see cref="Issue"/> and delivers exactly one
/// new-issue notification; repeat fingerprints only bump the occurrence count.
/// </summary>
/// <remarks>
/// Durable delivery: the new-issue notification's state is persisted on the issue itself
/// (a transactional outbox). An issue is persisted as <see cref="NewIssueNotificationStatus.Pending"/>
/// before any delivery is attempted, so a transient channel failure cannot silently suppress
/// the notification. Delivery is retried by <see cref="DeliverPendingNotificationsAsync"/> and
/// by any later ingestion of the same fingerprint.
/// </remarks>
public sealed class IssueIngestionService : IIssueIngestionService
{
    private readonly IIssueRepository _repository;
    private readonly IIssueNotifier _notifier;
    private readonly ILogger<IssueIngestionService> _logger;
    private readonly TimeProvider _timeProvider;

    public IssueIngestionService(
        IIssueRepository repository,
        IIssueNotifier notifier,
        ILogger<IssueIngestionService> logger,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<IngestionResult> IngestSignatureAsync(
        string signature,
        string fingerprint,
        DateTime occurredUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        var normalizedUtc = NormalizeToUtc(occurredUtc);

        var candidate = new Issue
        {
            Id = fingerprint,
            Fingerprint = fingerprint,
            Signature = signature,
            State = IssueState.New,
            OccurrenceCount = 1,
            FirstSeenUtc = normalizedUtc,
            LastSeenUtc = normalizedUtc,
            NotificationStatus = NewIssueNotificationStatus.Pending,
        };

        // Atomic create-or-get: exactly one concurrent caller is the creator; the rest
        // receive the stored instance and are treated as already-tracked occurrences.
        var claim = await _repository.GetOrAddAsync(candidate, cancellationToken).ConfigureAwait(false);
        var issue = claim.Issue;
        var outcome = claim.Created ? IngestionOutcome.New : IngestionOutcome.AlreadyTracked;

        if (!claim.Created)
        {
            issue.RecordOccurrence(signature, normalizedUtc);
            await _repository.UpdateAsync(issue, cancellationToken).ConfigureAwait(false);
        }

        // Deliver (or retry) the pending new-issue notification. Concurrent callers race on
        // the issue's delivery claim, so at most one notification is ever sent per issue.
        if (!issue.IsNotificationDelivered)
        {
            await TryDeliverNewIssueNotificationAsync(issue, cancellationToken).ConfigureAwait(false);
        }

        return new IngestionResult(outcome, issue);
    }

    public async Task<int> DeliverPendingNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _repository.ListPendingNewIssueNotificationsAsync(cancellationToken).ConfigureAwait(false);
        var delivered = 0;

        foreach (var issue in pending)
        {
            // Recover a claim abandoned by a crashed process before retrying.
            issue.RecoverStaleNotificationClaim();
            if (await TryDeliverNewIssueNotificationAsync(issue, cancellationToken).ConfigureAwait(false))
            {
                delivered++;
            }
        }

        return delivered;
    }

    private async Task<bool> TryDeliverNewIssueNotificationAsync(Issue issue, CancellationToken cancellationToken)
    {
        if (!issue.TryClaimNotificationDelivery())
        {
            return false;
        }

        try
        {
            var notification = new NewIssueNotification(
                Signature: issue.Signature,
                OccurrenceCount: issue.OccurrenceCount,
                FirstSeenUtc: issue.FirstSeenUtc,
                Fingerprint: issue.Fingerprint,
                TrackerUrl: TrackerUrl.ForFingerprint(issue.Fingerprint));

            await _notifier.NotifyNewIssueAsync(notification, cancellationToken).ConfigureAwait(false);

            issue.MarkNotificationDelivered();
            issue.NewIssueNotifiedUtc = _timeProvider.GetUtcNow().UtcDateTime;
            await _repository.UpdateAsync(issue, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            issue.ReleaseNotificationClaim();
            throw;
        }
        catch (Exception ex)
        {
            // Keep the issue persisted and the notification Pending so it can be retried;
            // the ingestion itself still succeeds.
            issue.ReleaseNotificationClaim();
            _logger.LogError(
                ex,
                "Delivery of the new-issue notification failed for fingerprint {Fingerprint}; " +
                "the notification remains pending and will be retried.",
                issue.Fingerprint);
            return false;
        }
    }

    private static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
