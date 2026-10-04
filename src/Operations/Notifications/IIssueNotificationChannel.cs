namespace Operations.Notifications;

/// <summary>
/// Pluggable transport for issue notifications (email, webhook, message-bus, log, ...).
/// </summary>
public interface IIssueNotificationChannel
{
    Task SendAsync(IssueNotificationMessage message, CancellationToken cancellationToken = default);
}
