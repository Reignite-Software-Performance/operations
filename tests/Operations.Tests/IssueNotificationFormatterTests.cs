using Operations.Notifications;
using Xunit;

namespace Operations.Tests;

public class IssueNotificationFormatterTests
{
    private static NewIssueNotification Sample() => new(
        Signature: "build-failed:MSB1003",
        OccurrenceCount: 3,
        FirstSeenUtc: new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Utc),
        Fingerprint: "6f607a64da64",
        TrackerUrl: "/tracker/6f607a64da64");

    [Fact]
    public void Format_IncludesAllFourRequiredDataPoints()
    {
        var message = IssueNotificationFormatter.Format(Sample());

        Assert.Contains("Signature: build-failed:MSB1003", message.Body);
        Assert.Contains("Occurrences: 3", message.Body);
        Assert.Contains("First seen (UTC): 2026-10-04T12:30:00.0000000Z", message.Body);
        Assert.Contains("Tracker: /tracker/6f607a64da64", message.Body);
    }

    [Fact]
    public void Format_SubjectMentionsSignature()
    {
        var message = IssueNotificationFormatter.Format(Sample());

        Assert.Contains("build-failed:MSB1003", message.Subject);
        Assert.Contains("New issue", message.Subject);
    }

    [Fact]
    public void Format_CarriesStructuredPayloadUnchanged()
    {
        var notification = Sample();

        var message = IssueNotificationFormatter.Format(notification);

        Assert.Same(notification, message.Payload);
    }

    [Fact]
    public void Format_RendersLocalOnsetAsUtc()
    {
        var localTime = new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Local);
        var notification = Sample() with { FirstSeenUtc = localTime };

        var message = IssueNotificationFormatter.Format(notification);

        Assert.Contains(localTime.ToUniversalTime().ToString("O"), message.Body);
    }

    [Fact]
    public void Format_NullNotification_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => IssueNotificationFormatter.Format(null!));
    }
}
