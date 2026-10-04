using System.Globalization;

namespace Operations.Notifications;

/// <summary>
/// Renders a <see cref="NewIssueNotification"/> into a human-readable message that
/// includes all four required data points.
/// </summary>
public static class IssueNotificationFormatter
{
    /// <summary>Converts the payload into a subject/body pair for human channels.</summary>
    public static IssueNotificationMessage Format(NewIssueNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var onset = notification.FirstSeenUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        var subject = $"[New issue] {notification.Signature}";
        var body = string.Join(
            Environment.NewLine,
            "A new issue has been detected and is now tracked.",
            $"Signature: {notification.Signature}",
            $"Occurrences: {notification.OccurrenceCount}",
            $"First seen (UTC): {onset}",
            $"Tracker: {notification.TrackerUrl}");

        return new IssueNotificationMessage(subject, body, notification);
    }
}
