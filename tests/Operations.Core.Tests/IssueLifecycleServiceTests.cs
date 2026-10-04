using Microsoft.EntityFrameworkCore;
using Operations.Core.Data;
using Operations.Core.Entities;
using Operations.Core.Services;
using Xunit;

namespace Operations.Core.Tests;

public class IssueLifecycleServiceTests
{
    private static OperationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OperationsDbContext(options);
    }

    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<(OperationsDbContext Context, IssueLifecycleService Service, Issue Issue)> SeedNewIssueAsync()
    {
        var context = CreateContext();
        var service = new IssueLifecycleService(context);
        var issue = await service.RecordOccurrenceAsync("fp-1", "NullReferenceException at Foo.Bar", T0);
        return (context, service, issue);
    }

    [Fact]
    public async Task RecordOccurrenceAsync_CreatesNewIssue_OnFirstOccurrence()
    {
        var (context, _, issue) = await SeedNewIssueAsync();

        Assert.Equal(IssueState.New, issue.State);
        Assert.Equal(1, issue.OccurrenceCount);
        Assert.Equal(1, await context.Issues.CountAsync());
    }

    [Fact]
    public async Task RecordOccurrenceAsync_IncrementsOccurrenceCount_OnRepeatOccurrence()
    {
        var (context, service, issue) = await SeedNewIssueAsync();
        var secondSeen = T0.AddHours(1);

        var updated = await service.RecordOccurrenceAsync("fp-1", issue.Signature, secondSeen);

        Assert.Equal(issue.Id, updated.Id);
        Assert.Equal(2, updated.OccurrenceCount);
        Assert.Equal(secondSeen, updated.LastSeenUtc);
        Assert.Equal(1, await context.Issues.CountAsync());
    }

    [Fact]
    public async Task TriageAsync_MovesNewToTriaged_AndStampsTimestamp()
    {
        var (_, service, issue) = await SeedNewIssueAsync();
        var when = T0.AddHours(1);

        var result = await service.TriageAsync(issue.Id, when);

        Assert.Equal(IssueState.Triaged, result.State);
        Assert.Equal(when, result.TriagedAtUtc);
    }

    [Fact]
    public async Task AcknowledgeAsync_MovesTriagedToAcknowledged_AndStampsTimestamp()
    {
        var (_, service, issue) = await SeedNewIssueAsync();
        await service.TriageAsync(issue.Id, T0.AddHours(1));
        var when = T0.AddHours(2);

        var result = await service.AcknowledgeAsync(issue.Id, when);

        Assert.Equal(IssueState.Acknowledged, result.State);
        Assert.Equal(when, result.AcknowledgedAtUtc);
    }

    [Fact]
    public async Task ResolveAsync_MovesAcknowledgedToResolved_AndStampsTimestamp()
    {
        var (_, service, issue) = await SeedNewIssueAsync();
        await service.TriageAsync(issue.Id, T0.AddHours(1));
        await service.AcknowledgeAsync(issue.Id, T0.AddHours(2));
        var when = T0.AddHours(3);

        var result = await service.ResolveAsync(issue.Id, when);

        Assert.Equal(IssueState.Resolved, result.State);
        Assert.Equal(when, result.ResolvedAtUtc);
    }

    [Fact]
    public async Task VerifyAsync_MovesResolvedToVerified_AndStampsTimestamp()
    {
        var (_, service, issue) = await SeedNewIssueAsync();
        await service.TriageAsync(issue.Id, T0.AddHours(1));
        await service.AcknowledgeAsync(issue.Id, T0.AddHours(2));
        await service.ResolveAsync(issue.Id, T0.AddHours(3));
        var when = T0.AddHours(4);

        var result = await service.VerifyAsync(issue.Id, when);

        Assert.Equal(IssueState.Verified, result.State);
        Assert.Equal(when, result.VerifiedAtUtc);
    }

    [Fact]
    public async Task ReopenAsync_MovesResolvedBackToNew_ForRegression()
    {
        var (_, service, issue) = await SeedNewIssueAsync();
        await service.TriageAsync(issue.Id, T0.AddHours(1));
        await service.AcknowledgeAsync(issue.Id, T0.AddHours(2));
        await service.ResolveAsync(issue.Id, T0.AddHours(3));
        var when = T0.AddHours(4);

        var result = await service.ReopenAsync(issue.Id, when);

        Assert.Equal(IssueState.New, result.State);
        // Downstream timestamps are cleared so a fresh pass records new ones.
        Assert.Null(result.TriagedAtUtc);
        Assert.Null(result.AcknowledgedAtUtc);
        Assert.Null(result.ResolvedAtUtc);
        Assert.Null(result.VerifiedAtUtc);
    }

    [Fact]
    public async Task TransitionAsync_Throws_ForNewToVerified()
    {
        var (_, service, issue) = await SeedNewIssueAsync();

        var ex = await Assert.ThrowsAsync<InvalidIssueTransitionException>(
            () => service.VerifyAsync(issue.Id, T0.AddHours(1)));

        Assert.Equal(IssueState.New, ex.FromState);
        Assert.Equal(IssueState.Verified, ex.ToState);
    }

    [Fact]
    public async Task TransitionAsync_Throws_ForTriagedToResolved_SkippingAcknowledged()
    {
        var (_, service, issue) = await SeedNewIssueAsync();
        await service.TriageAsync(issue.Id, T0.AddHours(1));

        await Assert.ThrowsAsync<InvalidIssueTransitionException>(
            () => service.ResolveAsync(issue.Id, T0.AddHours(2)));
    }

    [Fact]
    public async Task TransitionAsync_Throws_ForVerifiedToAnyState()
    {
        var (_, service, issue) = await SeedNewIssueAsync();
        await service.TriageAsync(issue.Id, T0.AddHours(1));
        await service.AcknowledgeAsync(issue.Id, T0.AddHours(2));
        await service.ResolveAsync(issue.Id, T0.AddHours(3));
        await service.VerifyAsync(issue.Id, T0.AddHours(4));

        await Assert.ThrowsAsync<InvalidIssueTransitionException>(
            () => service.TriageAsync(issue.Id, T0.AddHours(5)));
    }

    [Fact]
    public async Task TransitionAsync_Throws_WhenIssueDoesNotExist()
    {
        var context = CreateContext();
        var service = new IssueLifecycleService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TriageAsync(Guid.NewGuid(), T0));
    }
}
