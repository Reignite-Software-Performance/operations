using Operations.Core.Entities;

namespace Operations.Core.Services;

/// <summary>
/// Enforces the Issue lifecycle state machine and persists state transitions.
/// </summary>
public interface IIssueLifecycleService
{
    /// <summary>
    /// Records a new occurrence of an issue. If no issue exists for the given
    /// fingerprint, a new one is created in the <see cref="IssueState.New"/> state.
    /// If one already exists, its occurrence count and last-seen timestamp are
    /// updated instead of creating a duplicate row.
    /// </summary>
    Task<Issue> RecordOccurrenceAsync(string fingerprint, string signature, DateTime occurredAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an issue from Triaged into the Acknowledged state. Overload set
    /// below provides one method per allowed transition name for clarity, all
    /// delegating to <see cref="TransitionAsync"/>.
    /// </summary>
    Task<Issue> TriageAsync(Guid issueId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);

    Task<Issue> AcknowledgeAsync(Guid issueId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);

    Task<Issue> ResolveAsync(Guid issueId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);

    Task<Issue> VerifyAsync(Guid issueId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens a Resolved issue back to New, for regressions.
    /// </summary>
    Task<Issue> ReopenAsync(Guid issueId, DateTime occurredAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generic transition entry point. Throws <see cref="InvalidIssueTransitionException"/>
    /// if <paramref name="targetState"/> is not reachable from the issue's current state.
    /// </summary>
    Task<Issue> TransitionAsync(Guid issueId, IssueState targetState, DateTime occurredAtUtc, CancellationToken cancellationToken = default);
}
