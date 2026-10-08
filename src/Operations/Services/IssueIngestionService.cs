using Microsoft.EntityFrameworkCore;
using Operations.Data;
using Operations.Models;

namespace Operations.Services;

/// <summary>
/// Ingests incoming signatures and deduplicates them against persisted issues by fingerprint.
/// </summary>
public sealed class IssueIngestionService
{
    private const int MaxConcurrencyAttempts = 5;

    private readonly OperationsDbContext _db;

    public IssueIngestionService(OperationsDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// Ingests a signature. If an issue with the same <paramref name="fingerprint"/> already exists,
    /// its occurrence count is incremented and its last-seen time advanced (already-tracked);
    /// otherwise a new issue in the <see cref="IssueState.New"/> state is created.
    /// </summary>
    public async Task<IssueIngestionResult> IngestSignature(
        string signature,
        string fingerprint,
        DateTime occurredUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        var existing = await FindByFingerprint(fingerprint, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await RecordOccurrenceOnExisting(existing, occurredUtc, cancellationToken).ConfigureAwait(false);
        }

        var issue = Issue.CreateNew(signature, fingerprint, occurredUtc);
        _db.Issues.Add(issue);

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return IssueIngestionResult.New(issue);
        }
        catch (DbUpdateException)
        {
            // A concurrent ingestion created the same fingerprint between our read and write.
            // Detach our losing insert and fold the occurrence into the winning row instead.
            _db.Entry(issue).State = EntityState.Detached;

            var winner = await FindByFingerprint(fingerprint, cancellationToken).ConfigureAwait(false);
            if (winner is null)
            {
                // Not a fingerprint collision — surface the original failure.
                throw;
            }

            return await RecordOccurrenceOnExisting(winner, occurredUtc, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Increments the occurrence count on an already-tracked issue. The increment is a
    /// read-modify-write, so it is guarded by the <c>OccurrenceCount</c> concurrency token:
    /// if a concurrent ingestion advanced the row first the save conflicts, and we reload
    /// and re-apply the increment rather than losing it.
    /// </summary>
    private async Task<IssueIngestionResult> RecordOccurrenceOnExisting(
        Issue issue,
        DateTime occurredUtc,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            issue.RecordOccurrence(occurredUtc);

            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return IssueIngestionResult.AlreadyTracked(issue);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyAttempts)
            {
                // Another writer won the race: refresh our copy from the store and retry.
                await _db.Entry(issue).ReloadAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private Task<Issue?> FindByFingerprint(string fingerprint, CancellationToken cancellationToken) =>
        _db.Issues.FirstOrDefaultAsync(i => i.Fingerprint == fingerprint, cancellationToken);
}
