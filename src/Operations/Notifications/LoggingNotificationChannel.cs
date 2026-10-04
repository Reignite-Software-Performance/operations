using Microsoft.Extensions.Logging;

namespace Operations.Notifications;

/// <summary>
/// Fallback <see cref="IIssueNotificationChannel"/> used when no real transport (email,
/// webhook, message-bus) is configured. Writes the structured payload at Information
/// level so the notification is still observable.
/// </summary>
public sealed class LoggingNotificationChannel : IIssueNotificationChannel
{
    private readonly ILogger<LoggingNotificationChannel> _logger;

    public LoggingNotificationChannel(ILogger<LoggingNotificationChannel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SendAsync(IssueNotificationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = message.Payload;
        _logger.LogInformation(
            "New issue notified: Signature={Signature} OccurrenceCount={OccurrenceCount} " +
            "FirstSeenUtc={FirstSeenUtc:O} TrackerUrl={TrackerUrl} Fingerprint={Fingerprint}{NewLine}{Body}",
            payload.Signature,
            payload.OccurrenceCount,
            payload.FirstSeenUtc,
            payload.TrackerUrl,
            payload.Fingerprint,
            Environment.NewLine,
            message.Body);

        return Task.CompletedTask;
    }
}
