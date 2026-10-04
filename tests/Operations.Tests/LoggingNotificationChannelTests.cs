using Microsoft.Extensions.Logging;
using Operations.Notifications;
using Operations.Tests.Fakes;
using Xunit;

namespace Operations.Tests;

public class LoggingNotificationChannelTests
{
    private static IssueNotificationMessage SampleMessage() => IssueNotificationFormatter.Format(
        new NewIssueNotification(
            Signature: "build-failed:MSB1003",
            OccurrenceCount: 2,
            FirstSeenUtc: new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Utc),
            Fingerprint: "6f607a64da64",
            TrackerUrl: "/tracker/6f607a64da64"));

    private static LoggingNotificationChannel CreateChannel(CapturingLoggerProvider provider)
    {
        var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        return new LoggingNotificationChannel(factory.CreateLogger<LoggingNotificationChannel>());
    }

    [Fact]
    public async Task SendAsync_WritesStructuredPayloadAtInformationLevel()
    {
        var provider = new CapturingLoggerProvider();
        var channel = CreateChannel(provider);

        await channel.SendAsync(SampleMessage());

        var entry = Assert.Single(provider.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("Signature=build-failed:MSB1003", entry.Message);
        Assert.Contains("OccurrenceCount=2", entry.Message);
        Assert.Contains("FirstSeenUtc=2026-10-04T12:30:00.0000000Z", entry.Message);
        Assert.Contains("TrackerUrl=/tracker/6f607a64da64", entry.Message);
    }

    [Fact]
    public async Task SendAsync_IncludesHumanReadableBody()
    {
        var provider = new CapturingLoggerProvider();
        var channel = CreateChannel(provider);

        await channel.SendAsync(SampleMessage());

        var entry = Assert.Single(provider.Entries);
        Assert.Contains("Signature: build-failed:MSB1003", entry.Message);
        Assert.Contains("Tracker: /tracker/6f607a64da64", entry.Message);
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LoggingNotificationChannel(null!));
    }

    [Fact]
    public async Task SendAsync_NullMessage_Throws()
    {
        var channel = CreateChannel(new CapturingLoggerProvider());

        await Assert.ThrowsAsync<ArgumentNullException>(() => channel.SendAsync(null!));
    }
}
