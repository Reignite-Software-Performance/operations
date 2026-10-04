using Microsoft.Extensions.Logging.Abstractions;
using Operations.Ingestion;
using Operations.Issues;
using Operations.Notifications;
using Operations.Tests.Fakes;
using Xunit;

namespace Operations.Tests;

public class IssueIngestionServiceTests
{
    private static readonly DateTime Onset = new(2026, 10, 4, 12, 30, 0, DateTimeKind.Utc);

    private static (IssueIngestionService Service, RecordingIssueNotifier Notifier, IIssueRepository Repository) Create()
    {
        var repository = new InMemoryIssueRepository();
        var notifier = new RecordingIssueNotifier();
        return (Build(repository, notifier), notifier, repository);
    }

    private static IssueIngestionService Build(IIssueRepository repository, IIssueNotifier notifier)
        => new(repository, notifier, NullLogger<IssueIngestionService>.Instance, TimeProvider.System);

    [Fact]
    public async Task FirstIngestion_CreatesNewIssue_AndNotifiesExactlyOnce()
    {
        var (service, notifier, repository) = Create();

        var result = await service.IngestSignatureAsync("build-failed:MSB1003", "fp-1", Onset);

        Assert.Equal(IngestionOutcome.New, result.Outcome);
        Assert.True(result.IsNew);
        Assert.Equal(IssueState.New, result.Issue.State);
        Assert.Equal(1, result.Issue.OccurrenceCount);
        Assert.Equal(Onset, result.Issue.FirstSeenUtc);
        Assert.Equal(Onset, result.Issue.LastSeenUtc);
        Assert.Equal("fp-1", result.Issue.Id);

        var stored = await repository.FindByFingerprintAsync("fp-1");
        Assert.NotNull(stored);
        Assert.True(stored!.IsNotificationDelivered);

        var notification = Assert.Single(notifier.All);
        Assert.Equal("build-failed:MSB1003", notification.Signature);
        Assert.Equal(1, notification.OccurrenceCount);
        Assert.Equal(Onset, notification.FirstSeenUtc);
        Assert.Equal("/tracker/fp-1", notification.TrackerUrl);
    }

    [Fact]
    public async Task SecondIngestion_IsAlreadyTracked_AndDoesNotNotify()
    {
        var (service, notifier, _) = Create();
        await service.IngestSignatureAsync("sig", "fp-1", Onset);

        var later = Onset.AddMinutes(5);
        var result = await service.IngestSignatureAsync("sig", "fp-1", later);

        Assert.Equal(IngestionOutcome.AlreadyTracked, result.Outcome);
        Assert.False(result.IsNew);
        Assert.Equal(2, result.Issue.OccurrenceCount);
        Assert.Equal(Onset, result.Issue.FirstSeenUtc);
        Assert.Equal(later, result.Issue.LastSeenUtc);

        // Still exactly one new-issue notification.
        Assert.Equal(1, notifier.Count);
    }

    [Fact]
    public async Task DifferentFingerprints_CreateDistinctIssues()
    {
        var (service, notifier, repository) = Create();

        var first = await service.IngestSignatureAsync("sig-a", "fp-a", Onset);
        var second = await service.IngestSignatureAsync("sig-b", "fp-b", Onset.AddMinutes(1));

        Assert.Equal(IngestionOutcome.New, first.Outcome);
        Assert.Equal(IngestionOutcome.New, second.Outcome);
        Assert.NotEqual(first.Issue.Id, second.Issue.Id);
        Assert.Equal(2, (await repository.ListAsync()).Count);
        Assert.Equal(2, notifier.Count);
    }

    [Fact]
    public async Task NewIssueNotification_ContainsSignatureCountOnsetAndTrackerUrl()
    {
        var (service, notifier, _) = Create();

        await service.IngestSignatureAsync("compile-error:CS0246", "fingerprint-xyz", Onset);

        var notification = Assert.Single(notifier.All);
        Assert.Equal("compile-error:CS0246", notification.Signature);
        Assert.Equal(1, notification.OccurrenceCount);
        Assert.Equal(Onset, notification.FirstSeenUtc);
        Assert.Equal("/tracker/fingerprint-xyz", notification.TrackerUrl);
        Assert.Equal("fingerprint-xyz", notification.Fingerprint);
    }

    [Fact]
    public async Task IngestSignatureAsync_RejectsBlankInputs()
    {
        var (service, _, _) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() => service.IngestSignatureAsync("", "fp", Onset));
        await Assert.ThrowsAsync<ArgumentException>(() => service.IngestSignatureAsync("sig", "  ", Onset));
    }

    [Fact]
    public async Task IngestSignatureAsync_NormalizesUnspecifiedKindToUtc()
    {
        var (service, notifier, _) = Create();
        var unspecified = new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Unspecified);

        await service.IngestSignatureAsync("sig", "fp", unspecified);

        var notification = Assert.Single(notifier.All);
        Assert.Equal(DateTimeKind.Utc, notification.FirstSeenUtc.Kind);
    }

    [Fact]
    public async Task TransientDeliveryFailure_LeavesNotificationPending_AndRetryDeliversExactlyOnce()
    {
        var repository = new InMemoryIssueRepository();
        var notifier = new ControllableIssueNotifier(failuresBeforeSuccess: 1);
        var service = Build(repository, notifier);

        var result = await service.IngestSignatureAsync("sig", "fp-retry", Onset);

        // Ingestion still succeeds and the issue is persisted, but nothing was delivered yet.
        Assert.Equal(IngestionOutcome.New, result.Outcome);
        Assert.Empty(notifier.Delivered);
        Assert.False(result.Issue.IsNotificationDelivered);

        var delivered = await service.DeliverPendingNotificationsAsync();

        Assert.Equal(1, delivered);
        Assert.Single(notifier.Delivered);
        Assert.True(result.Issue.IsNotificationDelivered);

        // A second drain delivers nothing (no duplicate).
        Assert.Equal(0, await service.DeliverPendingNotificationsAsync());
        Assert.Single(notifier.Delivered);
    }

    [Fact]
    public async Task RepeatedDeliveryFailure_KeepsRetryingUntilSuccess()
    {
        var repository = new InMemoryIssueRepository();
        var notifier = new ControllableIssueNotifier(failuresBeforeSuccess: 3);
        var service = Build(repository, notifier);

        await service.IngestSignatureAsync("sig", "fp-retry", Onset);

        Assert.Equal(0, await service.DeliverPendingNotificationsAsync());
        Assert.Equal(0, await service.DeliverPendingNotificationsAsync());
        Assert.Empty(notifier.Delivered);

        Assert.Equal(1, await service.DeliverPendingNotificationsAsync());
        Assert.Single(notifier.Delivered);
        Assert.Equal(0, await service.DeliverPendingNotificationsAsync());
    }

    [Fact]
    public async Task ConcurrentIngestion_OfNewFingerprint_NotifiesExactlyOnce_AndCountsAllOccurrences()
    {
        const int callers = 24;
        var repository = new InMemoryIssueRepository();
        var notifier = new RecordingIssueNotifier();
        var service = Build(repository, notifier);

        var results = await Task.WhenAll(
            Enumerable.Range(0, callers).Select(_ =>
                Task.Run(() => service.IngestSignatureAsync("sig", "fp-race", Onset))));

        Assert.Equal(1, results.Count(r => r.Outcome == IngestionOutcome.New));
        Assert.Equal(callers - 1, results.Count(r => r.Outcome == IngestionOutcome.AlreadyTracked));
        Assert.Equal(1, notifier.Count);

        var stored = await repository.FindByFingerprintAsync("fp-race");
        Assert.NotNull(stored);
        Assert.Equal(callers, stored!.OccurrenceCount);
        Assert.True(stored.IsNotificationDelivered);
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new IssueIngestionService(
            null!, new RecordingIssueNotifier(), NullLogger<IssueIngestionService>.Instance, TimeProvider.System));
        Assert.Throws<ArgumentNullException>(() => new IssueIngestionService(
            new InMemoryIssueRepository(), null!, NullLogger<IssueIngestionService>.Instance, TimeProvider.System));
        Assert.Throws<ArgumentNullException>(() => new IssueIngestionService(
            new InMemoryIssueRepository(), new RecordingIssueNotifier(), null!, TimeProvider.System));
        Assert.Throws<ArgumentNullException>(() => new IssueIngestionService(
            new InMemoryIssueRepository(), new RecordingIssueNotifier(), NullLogger<IssueIngestionService>.Instance, null!));
    }
}
