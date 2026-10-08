using Microsoft.EntityFrameworkCore;
using Operations.Data;
using Operations.Models;
using Operations.Services;
using Xunit;

namespace Operations.Tests;

public class IssueIngestionServiceTests
{
    private static readonly DateTime Onset = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static OperationsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<OperationsDbContext>()
            .UseInMemoryDatabase($"ingest-{Guid.NewGuid()}")
            .Options);

    [Fact]
    public async Task FirstIngestion_OfFingerprint_CreatesNewIssue()
    {
        using var db = CreateContext();
        var service = new IssueIngestionService(db);

        var result = await service.IngestSignature("NullReferenceException in Orders", "fp-orders-nre", Onset);

        Assert.Equal(IngestionOutcome.New, result.Outcome);
        Assert.True(result.IsNew);
        Assert.False(result.IsAlreadyTracked);

        var issue = Assert.Single(await db.Issues.ToListAsync());
        Assert.Equal(IssueState.New, issue.State);
        Assert.Equal(1, issue.OccurrenceCount);
        Assert.Equal("fp-orders-nre", issue.Fingerprint);
        Assert.Equal("NullReferenceException in Orders", issue.Signature);
        Assert.Equal(Onset, issue.FirstSeenUtc);
        Assert.Equal(Onset, issue.LastSeenUtc);
        Assert.Equal(Issue.DeriveId("fp-orders-nre"), issue.Id);
    }

    [Fact]
    public async Task SecondIngestion_OfSameFingerprint_IncrementsAndDoesNotCreateDuplicate()
    {
        using var db = CreateContext();
        var service = new IssueIngestionService(db);

        await service.IngestSignature("boom", "fp-same", Onset);
        var later = Onset.AddMinutes(5);

        var result = await service.IngestSignature("boom", "fp-same", later);

        Assert.Equal(IngestionOutcome.AlreadyTracked, result.Outcome);
        Assert.True(result.IsAlreadyTracked);
        Assert.False(result.IsNew);

        var issue = Assert.Single(await db.Issues.ToListAsync());
        Assert.Equal(2, issue.OccurrenceCount);
        Assert.Equal(Onset, issue.FirstSeenUtc); // onset preserved
        Assert.Equal(later, issue.LastSeenUtc);  // advanced
        Assert.Equal(later, issue.UpdatedUtc);
    }

    [Fact]
    public async Task DifferentFingerprints_ProduceTwoDistinctIssues()
    {
        using var db = CreateContext();
        var service = new IssueIngestionService(db);

        var first = await service.IngestSignature("sig-a", "fp-a", Onset);
        var second = await service.IngestSignature("sig-b", "fp-b", Onset.AddSeconds(1));

        Assert.Equal(IngestionOutcome.New, first.Outcome);
        Assert.Equal(IngestionOutcome.New, second.Outcome);
        Assert.NotEqual(first.Issue.Id, second.Issue.Id);

        var issues = await db.Issues.OrderBy(i => i.Fingerprint).ToListAsync();
        Assert.Equal(2, issues.Count);
        Assert.Equal("fp-a", issues[0].Fingerprint);
        Assert.Equal("fp-b", issues[1].Fingerprint);
        Assert.All(issues, i => Assert.Equal(1, i.OccurrenceCount));
    }

    [Fact]
    public async Task RepeatedIngestion_AccumulatesOccurrencesOnOneIssue()
    {
        using var db = CreateContext();
        var service = new IssueIngestionService(db);

        for (var i = 0; i < 10; i++)
        {
            var result = await service.IngestSignature("boom", "fp-repeat", Onset.AddMinutes(i));
            Assert.Equal(i == 0 ? IngestionOutcome.New : IngestionOutcome.AlreadyTracked, result.Outcome);
        }

        var issue = Assert.Single(await db.Issues.ToListAsync());
        Assert.Equal(10, issue.OccurrenceCount);
        Assert.Equal(Onset, issue.FirstSeenUtc);
        Assert.Equal(Onset.AddMinutes(9), issue.LastSeenUtc);
    }

    [Fact]
    public async Task MatchingIsExact_SimilarFingerprintsStaySeparate()
    {
        using var db = CreateContext();
        var service = new IssueIngestionService(db);

        await service.IngestSignature("sig", "fp-1", Onset);
        await service.IngestSignature("sig", "fp-11", Onset);
        await service.IngestSignature("sig", "fp-1 ", Onset);

        Assert.Equal(3, await db.Issues.CountAsync());
    }

    [Theory]
    [InlineData("", "fp")]
    [InlineData("   ", "fp")]
    [InlineData("sig", "")]
    [InlineData("sig", "   ")]
    public async Task BlankArguments_Throw(string signature, string fingerprint)
    {
        using var db = CreateContext();
        var service = new IssueIngestionService(db);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.IngestSignature(signature, fingerprint, Onset));
    }

    [Fact]
    public void Constructor_NullContext_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IssueIngestionService(null!));
    }
}
