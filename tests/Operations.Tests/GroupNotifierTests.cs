using Microsoft.Extensions.Logging.Abstractions;
using Operations.Notifications;
using Operations.Tests.Fakes;
using Xunit;

namespace Operations.Tests;

public class GroupNotifierTests
{
    private static NewIssueNotification Sample() => new(
        Signature: "build-failed:MSB1003",
        OccurrenceCount: 1,
        FirstSeenUtc: new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Utc),
        Fingerprint: "6f607a64da64",
        TrackerUrl: "/tracker/6f607a64da64");

    [Fact]
    public async Task NotifyNewIssueAsync_SendsExactlyOneMessageWithAllDataPoints()
    {
        var channel = new RecordingNotificationChannel();
        var notifier = new GroupNotifier(channel, NullLogger<GroupNotifier>.Instance);

        await notifier.NotifyNewIssueAsync(Sample());

        var message = Assert.Single(channel.All);
        Assert.Contains("build-failed:MSB1003", message.Body);
        Assert.Contains("Occurrences: 1", message.Body);
        Assert.Contains("First seen (UTC): 2026-10-04T12:30:00.0000000Z", message.Body);
        Assert.Contains("/tracker/6f607a64da64", message.Body);
    }

    [Fact]
    public async Task NotifyNewIssueAsync_PreservesStructuredPayload()
    {
        var channel = new RecordingNotificationChannel();
        var notifier = new GroupNotifier(channel, NullLogger<GroupNotifier>.Instance);
        var notification = Sample();

        await notifier.NotifyNewIssueAsync(notification);

        var message = Assert.Single(channel.All);
        Assert.Same(notification, message.Payload);
    }

    [Fact]
    public void Constructor_NullChannel_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GroupNotifier(null!, NullLogger<GroupNotifier>.Instance));
    }

    [Fact]
    public async Task NotifyNewIssueAsync_NullNotification_Throws()
    {
        var notifier = new GroupNotifier(new RecordingNotificationChannel(), NullLogger<GroupNotifier>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() => notifier.NotifyNewIssueAsync(null!));
    }
}
