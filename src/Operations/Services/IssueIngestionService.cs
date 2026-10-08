using Microsoft.EntityFrameworkCore;
using Operations.Data;
using Operations.Models;

namespace Operations.Services;

/// <summary>
/// Ingests incoming signatures and deduplicates them against persisted issues by fingerprint.
/// </summary>
public sealed class IssueIngestionService
{
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
            existing.RecordOccurrence(occurredUtc);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return IssueIngestionResult.AlreadyTracked(existing);
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

            winner.RecordOccurrence(occurredUtc);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return IssueIngestionResult.AlreadyTracked(winner);
        }
    }

    private Task<Issue?> FindByFingerprint(string fingerprint, CancellationToken cancellationToken) =>
        _db.Issues.FirstOrDefaultAsync(i => i.Fingerprint == fingerprint, cancellationToken);
}
