using Microsoft.EntityFrameworkCore;
using Operations.Core.Data;
using Operations.Core.Entities;
using Operations.Core.Services;
using Xunit;

namespace Operations.Core.Tests;

/// <summary>
/// End-to-end functional tests that exercise the full Issue lifecycle through
/// the EF Core-backed <see cref="IssueLifecycleService"/>, persisting and
/// re-reading from the database between every transition — the same pattern a
/// real caller (a web API, a background worker) would follow.
/// </summary>
public class IssueLifecyclePersistenceFunctionalTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FullLifecycle_PersistsEveryTransitionAndTimestamp_AcrossSeparateDbContextInstances()
    {
        var databaseName = Guid.NewGuid().ToString();
        DbContextOptions<OperationsDbContext> OptionsFor(string name) =>
            new DbContextOptionsBuilder<OperationsDbContext>().UseInMemoryDatabase(name).Options;

        // Step 1: a new occurrence arrives and is persisted as a brand new Issue.
        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var service = new IssueLifecycleService(context);
            await service.RecordOccurrenceAsync("fp-functional", "NullReferenceException at Checkout.Pay", T0);
        }

        var issueId = Issue.DeriveId("fp-functional");

        // Step 2: a repeat occurrence arrives from a different context instance —
        // it must be deduplicated against the persisted row, not create a second one.
        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var service = new IssueLifecycleService(context);
            await service.RecordOccurrenceAsync("fp-functional", "NullReferenceException at Checkout.Pay", T0.AddMinutes(5));
        }

        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var persisted = await context.Issues.SingleAsync(i => i.Id == issueId);
            Assert.Equal(2, persisted.OccurrenceCount);
            Assert.Equal(IssueState.New, persisted.State);
        }

        // Step 3: walk through the full valid lifecycle, one persisted transition at a time.
        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var service = new IssueLifecycleService(context);
            await service.TriageAsync(issueId, T0.AddHours(1));
        }

        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var service = new IssueLifecycleService(context);
            await service.AcknowledgeAsync(issueId, T0.AddHours(2));
        }

        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var service = new IssueLifecycleService(context);
            await service.ResolveAsync(issueId, T0.AddHours(3));
        }

        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var service = new IssueLifecycleService(context);
            await service.VerifyAsync(issueId, T0.AddHours(4));
        }

        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var finalState = await context.Issues.SingleAsync(i => i.Id == issueId);

            Assert.Equal(IssueState.Verified, finalState.State);
            Assert.Equal(T0.AddHours(1), finalState.TriagedAtUtc);
            Assert.Equal(T0.AddHours(2), finalState.AcknowledgedAtUtc);
            Assert.Equal(T0.AddHours(3), finalState.ResolvedAtUtc);
            Assert.Equal(T0.AddHours(4), finalState.VerifiedAtUtc);
            Assert.Equal(2, finalState.OccurrenceCount);
        }
    }

    [Fact]
    public async Task RegressionReopen_IsPersisted_AfterResolvedAndThenRecurs()
    {
        var databaseName = Guid.NewGuid().ToString();
        DbContextOptions<OperationsDbContext> OptionsFor(string name) =>
            new DbContextOptionsBuilder<OperationsDbContext>().UseInMemoryDatabase(name).Options;

        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var service = new IssueLifecycleService(context);
            await service.RecordOccurrenceAsync("fp-regression", "TimeoutException at Payments.Charge", T0);
        }

        var issueId = Issue.DeriveId("fp-regression");

        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var service = new IssueLifecycleService(context);
            await service.TriageAsync(issueId, T0.AddHours(1));
            await service.AcknowledgeAsync(issueId, T0.AddHours(2));
            await service.ResolveAsync(issueId, T0.AddHours(3));
        }

        // Regression: issue recurs after being Resolved, so it must reopen to New.
        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var service = new IssueLifecycleService(context);
            await service.ReopenAsync(issueId, T0.AddHours(4));
        }

        await using (var context = new OperationsDbContext(OptionsFor(databaseName)))
        {
            var reopened = await context.Issues.SingleAsync(i => i.Id == issueId);

            Assert.Equal(IssueState.New, reopened.State);
            Assert.Null(reopened.ResolvedAtUtc);

            // And from New it must be possible to walk the lifecycle again.
            var service = new IssueLifecycleService(context);
            var retriaged = await service.TriageAsync(issueId, T0.AddHours(5));
            Assert.Equal(IssueState.Triaged, retriaged.State);
        }
    }

    [Fact]
    public async Task InvalidTransition_IsRejected_AndDoesNotMutatePersistedState()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Issue created;
        await using (var context = new OperationsDbContext(options))
        {
            var service = new IssueLifecycleService(context);
            created = await service.RecordOccurrenceAsync("fp-invalid", "ArgumentException at Orders.Create", T0);
        }

        await using (var context = new OperationsDbContext(options))
        {
            var service = new IssueLifecycleService(context);
            await Assert.ThrowsAsync<InvalidIssueTransitionException>(
                () => service.AcknowledgeAsync(created.Id, T0.AddHours(1)));
        }

        await using (var context = new OperationsDbContext(options))
        {
            var unchanged = await context.Issues.SingleAsync(i => i.Id == created.Id);
            Assert.Equal(IssueState.New, unchanged.State);
            Assert.Null(unchanged.AcknowledgedAtUtc);
        }
    }
}
