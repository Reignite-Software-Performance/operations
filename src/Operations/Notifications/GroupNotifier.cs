using Microsoft.Extensions.Logging;

namespace Operations.Notifications;

/// <summary>
/// Default <see cref="IIssueNotifier"/>. Renders the notification to a human-readable
/// message and dispatches it through the configured <see cref="IIssueNotificationChannel"/>.
/// </summary>
public sealed class GroupNotifier : IIssueNotifier
{
    private readonly IIssueNotificationChannel _channel;
    private readonly ILogger<GroupNotifier> _logger;

    public GroupNotifier(IIssueNotificationChannel channel, ILogger<GroupNotifier> logger)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyNewIssueAsync(NewIssueNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var message = IssueNotificationFormatter.Format(notification);
        _logger.LogDebug(
            "Dispatching new-issue notification for fingerprint {Fingerprint}.",
            notification.Fingerprint);

        await _channel.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
