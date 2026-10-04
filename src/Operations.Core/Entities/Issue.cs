using System.Security.Cryptography;
using System.Text;

namespace Operations.Core.Entities;

/// <summary>
/// Represents a deduplicated issue detected by the operations pipeline, tracked
/// through a five-state lifecycle: New → Triaged → Acknowledged → Resolved → Verified,
/// with support for a Resolved → New regression transition.
/// </summary>
public class Issue
{
    /// <summary>
    /// Deterministic identifier derived from <see cref="Fingerprint"/>. Using a
    /// deterministic id (rather than a random Guid) means the same underlying
    /// occurrence always maps to the same Issue row, which is what allows
    /// occurrences to be deduplicated/upserted by fingerprint.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Stable identifier for the underlying occurrence (e.g. a hash of stack trace,
    /// error code, or location) used to deduplicate repeated occurrences into a
    /// single Issue.
    /// </summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable signature describing the issue (e.g. exception type + message).
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    public IssueState State { get; set; } = IssueState.New;

    /// <summary>
    /// Number of times this issue (by fingerprint) has been observed.
    /// </summary>
    public int OccurrenceCount { get; set; } = 1;

    public DateTime FirstSeenUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    /// <summary>
    /// Optional reference to the feature/task that owns remediation of this issue.
    /// </summary>
    public Guid? LinkedRemediationId { get; set; }

    public DateTime? TriagedAtUtc { get; set; }

    public DateTime? AcknowledgedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public DateTime? VerifiedAtUtc { get; set; }

    /// <summary>
    /// Derives a deterministic <see cref="Guid"/> from a fingerprint so the same
    /// fingerprint always produces the same Issue id.
    /// </summary>
    public static Guid DeriveId(string fingerprint)
    {
        if (string.IsNullOrEmpty(fingerprint))
        {
            throw new ArgumentException("Fingerprint must not be null or empty.", nameof(fingerprint));
        }

        var hash = MD5.HashData(Encoding.UTF8.GetBytes(fingerprint));

        // MD5 produces 16 bytes, which is exactly the size of a Guid.
        return new Guid(hash);
    }

    /// <summary>
    /// Creates a new Issue for a first-observed occurrence, with a deterministic
    /// id derived from the fingerprint.
    /// </summary>
    public static Issue Create(string fingerprint, string signature, DateTime occurredAtUtc)
    {
        return new Issue
        {
            Id = DeriveId(fingerprint),
            Fingerprint = fingerprint,
            Signature = signature,
            State = IssueState.New,
            OccurrenceCount = 1,
            FirstSeenUtc = occurredAtUtc,
            LastSeenUtc = occurredAtUtc
        };
    }
}
