using System.Security.Cryptography;
using System.Text;

namespace Operations.Models;

/// <summary>
/// A tracked issue, uniquely identified by the fingerprint of the incoming signature.
/// The identity is deterministic: the same fingerprint always maps to the same <see cref="Id"/>.
/// </summary>
public sealed class Issue
{
    /// <summary>Deterministic identifier derived from <see cref="Fingerprint"/>.</summary>
    public Guid Id { get; private set; }

    /// <summary>Deduplication key for the originating signature.</summary>
    public string Fingerprint { get; private set; } = string.Empty;

    /// <summary>Human-readable signature text that produced this issue.</summary>
    public string Signature { get; private set; } = string.Empty;

    public IssueState State { get; private set; }

    public int OccurrenceCount { get; private set; }

    public DateTime FirstSeenUtc { get; private set; }

    public DateTime LastSeenUtc { get; private set; }

    /// <summary>Optional reference to the feature/task that remediates this issue.</summary>
    public string? LinkedRemediationId { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime UpdatedUtc { get; private set; }

    // Required by EF Core materialisation.
    private Issue()
    {
    }

    /// <summary>
    /// Creates a brand-new issue in the <see cref="IssueState.New"/> state with an occurrence count of 1.
    /// </summary>
    public static Issue CreateNew(string signature, string fingerprint, DateTime occurredUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        var occurred = UtcTimestamps.Normalize(occurredUtc);

        return new Issue
        {
            Id = DeriveId(fingerprint),
            Fingerprint = fingerprint,
            Signature = signature,
            State = IssueState.New,
            OccurrenceCount = 1,
            FirstSeenUtc = occurred,
            LastSeenUtc = occurred,
            CreatedUtc = occurred,
            UpdatedUtc = occurred,
        };
    }

    /// <summary>
    /// Records a further occurrence of the same fingerprint. <see cref="FirstSeenUtc"/> is preserved;
    /// <see cref="LastSeenUtc"/> and <see cref="UpdatedUtc"/> advance to the new event time.
    /// </summary>
    public void RecordOccurrence(DateTime occurredUtc)
    {
        var occurred = UtcTimestamps.Normalize(occurredUtc);
        OccurrenceCount++;
        LastSeenUtc = occurred;
        UpdatedUtc = occurred;
    }

    /// <summary>Derives a stable <see cref="Guid"/> from a fingerprint.</summary>
    public static Guid DeriveId(string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint));
        return new Guid(hash.AsSpan(0, 16));
    }
}
