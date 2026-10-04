using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Operations.Data;
using Operations.Services;
using Xunit;

namespace Operations.Tests;

/// <summary>
/// End-to-end ingestion against a real relational provider (SQLite in-memory), so the
/// unique fingerprint index and cross-context persistence are genuinely exercised.
/// </summary>
public sealed class IssueIngestionFunctionalTests : IDisposable
{
    private static readonly DateTime Onset = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<OperationsDbContext> _options;

    public IssueIngestionFunctionalTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var setup = new OperationsDbContext(_options);
        setup.Database.EnsureCreated();
    }

    private OperationsDbContext NewContext() => new(_options);

    [Fact]
    public async Task Ingest_PersistsDedupeAcrossContexts_EndToEnd()
    {
        // First ingestion, in its own unit of work.
        await using (var db = NewContext())
        {
            var result = await new IssueIngestionService(db)
                .IngestSignature("boom", "fp-e2e", Onset);
            Assert.True(result.IsNew);
        }

        // Second ingestion of the same fingerprint, on a fresh context: must fold in, not duplicate.
        await using (var db = NewContext())
        {
            var result = await new IssueIngestionService(db)
                .IngestSignature("boom", "fp-e2e", Onset.AddMinutes(2));
            Assert.True(result.IsAlreadyTracked);
        }

        // A different fingerprint is tracked separately.
        await using (var db = NewContext())
        {
            var result = await new IssueIngestionService(db)
                .IngestSignature("other", "fp-e2e-other", Onset.AddMinutes(3));
            Assert.True(result.IsNew);
        }

        await using (var db = NewContext())
        {
            var issues = await db.Issues.AsNoTracking().ToListAsync();
            Assert.Equal(2, issues.Count);

            var tracked = Assert.Single(issues, i => i.Fingerprint == "fp-e2e");
            Assert.Equal(2, tracked.OccurrenceCount);
            Assert.Equal(Onset, tracked.FirstSeenUtc);
            Assert.Equal(Onset.AddMinutes(2), tracked.LastSeenUtc);

            var other = Assert.Single(issues, i => i.Fingerprint == "fp-e2e-other");
            Assert.Equal(1, other.OccurrenceCount);
        }
    }

    [Fact]
    public async Task Fingerprint_UniqueIndex_RejectsDuplicateRow()
    {
        await using (var seed = NewContext())
        {
            await new IssueIngestionService(seed).IngestSignature("boom", "fp-unique", Onset);
        }

        // Bypass the service on a fresh context to prove the database itself enforces the dedupe key.
        await using var db = NewContext();
        db.Issues.Add(Operations.Models.Issue.CreateNew("boom", "fp-unique", Onset));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    public void Dispose() => _connection.Dispose();
}
