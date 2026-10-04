using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Operations;
using Operations.Ingestion;
using Operations.Issues;
using Operations.Notifications;
using Operations.Tests.Fakes;
using Xunit;

namespace Operations.Tests;

/// <summary>
/// End-to-end coverage of the new-issue notification path: ingestion through the DI
/// container, deduplication, and the content/shape of the delivered notification.
/// </summary>
public class NewIssueNotificationIntegrationTests
{
    private static readonly DateTime Onset = new(2026, 10, 4, 12, 30, 0, DateTimeKind.Utc);

    private static ServiceProvider BuildProvider(IIssueNotificationChannel? channel = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (channel is not null)
        {
            services.AddSingleton(channel);
        }

        services.AddIssueTracker();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task NewFingerprint_DeliversExactlyOneNotificationWithFullPayload()
    {
        var channel = new RecordingNotificationChannel();
        using var provider = BuildProvider(channel);
        var ingestion = provider.GetRequiredService<IIssueIngestionService>();

        var result = await ingestion.IngestSignatureAsync("build-failed:MSB1003", "6f607a64da64", Onset);

        Assert.Equal(IngestionOutcome.New, result.Outcome);

        var message = Assert.Single(channel.All);
        Assert.Equal("build-failed:MSB1003", message.Payload.Signature);
        Assert.Equal(1, message.Payload.OccurrenceCount);
        Assert.Equal(Onset, message.Payload.FirstSeenUtc);
        Assert.Equal("/tracker/6f607a64da64", message.Payload.TrackerUrl);

        // Human-readable body carries all four data points.
        Assert.Contains("build-failed:MSB1003", message.Body);
        Assert.Contains("Occurrences: 1", message.Body);
        Assert.Contains("2026-10-04T12:30:00.0000000Z", message.Body);
        Assert.Contains("/tracker/6f607a64da64", message.Body);
    }

    [Fact]
    public async Task RepeatFingerprint_DoesNotDeliverAdditionalNotification()
    {
        var channel = new RecordingNotificationChannel();
        using var provider = BuildProvider(channel);
        var ingestion = provider.GetRequiredService<IIssueIngestionService>();

        await ingestion.IngestSignatureAsync("sig", "fp-repeat", Onset);
        var second = await ingestion.IngestSignatureAsync("sig", "fp-repeat", Onset.AddMinutes(2));
        var third = await ingestion.IngestSignatureAsync("sig", "fp-repeat", Onset.AddMinutes(4));

        Assert.Equal(IngestionOutcome.AlreadyTracked, second.Outcome);
        Assert.Equal(IngestionOutcome.AlreadyTracked, third.Outcome);
        Assert.Equal(3, third.Issue.OccurrenceCount);
        Assert.Equal(1, channel.Count);
    }

    [Fact]
    public async Task TwoFingerprints_DeliverTwoNotifications()
    {
        var channel = new RecordingNotificationChannel();
        using var provider = BuildProvider(channel);
        var ingestion = provider.GetRequiredService<IIssueIngestionService>();

        await ingestion.IngestSignatureAsync("sig-a", "fp-a", Onset);
        await ingestion.IngestSignatureAsync("sig-b", "fp-b", Onset.AddMinutes(1));

        Assert.Equal(2, channel.Count);
        Assert.Contains(channel.All, m => m.Payload.TrackerUrl == "/tracker/fp-a");
        Assert.Contains(channel.All, m => m.Payload.TrackerUrl == "/tracker/fp-b");
    }

    [Fact]
    public async Task DefaultChannel_FallsBackToStructuredInformationLog()
    {
        var loggerProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddIssueTracker();

        using var provider = services.BuildServiceProvider();
        var ingestion = provider.GetRequiredService<IIssueIngestionService>();

        await ingestion.IngestSignatureAsync("compile-error:CS0246", "fp-log", Onset);

        var entry = Assert.Single(
            loggerProvider.Entries,
            e => e.Level == LogLevel.Information && e.Category.Contains(nameof(LoggingNotificationChannel)));
        Assert.Contains("Signature=compile-error:CS0246", entry.Message);
        Assert.Contains("OccurrenceCount=1", entry.Message);
        Assert.Contains("FirstSeenUtc=2026-10-04T12:30:00.0000000Z", entry.Message);
        Assert.Contains("TrackerUrl=/tracker/fp-log", entry.Message);
    }

    [Fact]
    public async Task TrackerRoute_IsStableForSameFingerprintAcrossRestarts()
    {
        const string fingerprint = "stable-fingerprint";

        var firstChannel = new RecordingNotificationChannel();
        using (var firstProvider = BuildProvider(firstChannel))
        {
            await firstProvider.GetRequiredService<IIssueIngestionService>()
                .IngestSignatureAsync("sig", fingerprint, Onset);
        }

        var secondChannel = new RecordingNotificationChannel();
        using (var secondProvider = BuildProvider(secondChannel))
        {
            await secondProvider.GetRequiredService<IIssueIngestionService>()
                .IngestSignatureAsync("sig", fingerprint, Onset);
        }

        var first = Assert.Single(firstChannel.All).Payload.TrackerUrl;
        var second = Assert.Single(secondChannel.All).Payload.TrackerUrl;
        Assert.Equal(first, second);
        Assert.Equal(TrackerUrl.ForFingerprint(fingerprint), first);
    }

    [Fact]
    public async Task TransientFailure_IsRetriedThroughDI_AndDeliveredExactlyOnce()
    {
        var notifier = new ControllableIssueNotifier(failuresBeforeSuccess: 1);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IIssueNotifier>(notifier);
        services.AddIssueTracker();

        using var provider = services.BuildServiceProvider();
        var ingestion = provider.GetRequiredService<IIssueIngestionService>();

        var result = await ingestion.IngestSignatureAsync("sig", "fp-durable", Onset);

        Assert.True(result.IsNew);
        Assert.Empty(notifier.Delivered);

        // The pending notification survives the failure and is delivered on retry.
        Assert.Equal(1, await ingestion.DeliverPendingNotificationsAsync());
        var delivered = Assert.Single(notifier.Delivered);
        Assert.Equal("/tracker/fp-durable", delivered.TrackerUrl);
        Assert.Equal(0, await ingestion.DeliverPendingNotificationsAsync());
    }
}
