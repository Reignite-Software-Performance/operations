namespace Operations.Notifications;

/// <summary>
/// Sends new-issue notifications to the responsible group. Registered in DI so the
/// implementation (or the underlying channel) can be swapped without touching ingestion.
/// </summary>
public interface IIssueNotifier
{
    /// <summary>
    /// Delivers exactly one notification for a newly-created issue.
    /// </summary>
    Task NotifyNewIssueAsync(NewIssueNotification notification, CancellationToken cancellationToken = default);
}
