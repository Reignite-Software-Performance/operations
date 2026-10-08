using System.Security.Cryptography;
using System.Text;

namespace Operations.Models;

/// <summary>
/// A tracked issue, uniquely identified by the fingerprint of the incoming signature.
/// The identity is deterministic: the same fingerprint always maps to the same <see cref="Id"/>.
/// </summary>
public sealed class Issue
{
    /// <summary>
    /// The allowed transitions of the lifecycle state machine:
    /// New → Triaged → Acknowledged → Resolved → Verified, plus Resolved → New for regressions.
    /// </summary>
    private static readonly IReadOnlyDictionary<IssueState, IssueState[]> AllowedTransitions =
        new Dictionary<IssueState, IssueState[]>
        {
            [IssueState.New] = new[] { IssueState.Triaged },
            [IssueState.Triaged] = new[] { IssueState.Acknowledged },
            [IssueState.Acknowledged] = new[] { IssueState.Resolved },
            [IssueState.Resolved] = new[] { IssueState.Verified, IssueState.New },
            [IssueState.Verified] = Array.Empty<IssueState>(),
        };

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

    /// <summary>When the issue entered <see cref="IssueState.Triaged"/>, if it ever has.</summary>
    public DateTime? TriagedAtUtc { get; private set; }

    /// <summary>When the issue entered <see cref="IssueState.Acknowledged"/>, if it ever has.</summary>
    public DateTime? AcknowledgedAtUtc { get; private set; }

    /// <summary>When the issue entered <see cref="IssueState.Resolved"/>, if it ever has.</summary>
    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>When the issue entered <see cref="IssueState.Verified"/>, if it ever has.</summary>
    public DateTime? VerifiedAtUtc { get; private set; }

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
    /// <see cref="LastSeenUtc"/> and <see cref="UpdatedUtc"/> only ever move forward, so an
    /// out-of-order (older) event cannot rewind them.
    /// </summary>
    public void RecordOccurrence(DateTime occurredUtc)
    {
        var occurred = UtcTimestamps.Normalize(occurredUtc);
        OccurrenceCount++;

        if (occurred > LastSeenUtc)
        {
            LastSeenUtc = occurred;
        }

        if (occurred > UpdatedUtc)
        {
            UpdatedUtc = occurred;
        }
    }

    /// <summary>
    /// Transitions the issue to <paramref name="targetState"/> if that move is permitted by the
    /// lifecycle state machine (New → Triaged → Acknowledged → Resolved → Verified, plus
    /// Resolved → New for regressions) and stamps the matching transition timestamp.
    /// </summary>
    /// <exception cref="InvalidIssueTransitionException">
    /// Thrown when <paramref name="targetState"/> is not reachable from the current state.
    /// </exception>
    public void TransitionTo(IssueState targetState, DateTime occurredUtc)
    {
        if (!AllowedTransitions.TryGetValue(State, out var allowedTargets) || !allowedTargets.Contains(targetState))
        {
            throw new InvalidIssueTransitionException(State, targetState);
        }

        var occurred = UtcTimestamps.Normalize(occurredUtc);
        State = targetState;

        switch (targetState)
        {
            case IssueState.Triaged:
                TriagedAtUtc = occurred;
                break;
            case IssueState.Acknowledged:
                AcknowledgedAtUtc = occurred;
                break;
            case IssueState.Resolved:
                ResolvedAtUtc = occurred;
                break;
            case IssueState.Verified:
                VerifiedAtUtc = occurred;
                break;
            case IssueState.New:
                // Regression: reopen the issue and clear downstream timestamps so the
                // next pass through the lifecycle records fresh ones.
                TriagedAtUtc = null;
                AcknowledgedAtUtc = null;
                ResolvedAtUtc = null;
                VerifiedAtUtc = null;
                break;
        }

        if (occurred > UpdatedUtc)
        {
            UpdatedUtc = occurred;
        }
    }

    /// <summary>Derives a stable <see cref="Guid"/> from a fingerprint.</summary>
    public static Guid DeriveId(string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint));
        return new Guid(hash.AsSpan(0, 16));
    }
}
