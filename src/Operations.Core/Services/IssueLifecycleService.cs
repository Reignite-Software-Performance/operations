using Microsoft.EntityFrameworkCore;
using Operations.Core.Data;
using Operations.Core.Entities;

namespace Operations.Core.Services;

/// <summary>
/// Default <see cref="IIssueLifecycleService"/> implementation backed by
/// <see cref="OperationsDbContext"/>.
/// </summary>
public class IssueLifecycleService : IIssueLifecycleService
{
    /// <summary>
    /// Map of allowed transitions: for each source state, the set of target
    /// states reachable directly from it.
    /// </summary>
    private static readonly Dictionary<IssueState, IssueState[]> AllowedTransitions = new()
    {
        [IssueState.New] = new[] { IssueState.Triaged },
        [IssueState.Triaged] = new[] { IssueState.Acknowledged },
        [IssueState.Acknowledged] = new[] { IssueState.Resolved },
        [IssueState.Resolved] = new[] { IssueState.Verified, IssueState.New },
        [IssueState.Verified] = Array.Empty<IssueState>()
    };

    private readonly OperationsDbContext _dbContext;

    public IssueLifecycleService(OperationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Issue> RecordOccurrenceAsync(string fingerprint, string signature, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new ArgumentException("Fingerprint must not be null or empty.", nameof(fingerprint));
        }

        var id = Issue.DeriveId(fingerprint);
        var existing = await _dbContext.Issues.FirstOrDefaultAsync(issue => issue.Id == id, cancellationToken);

        if (existing is null)
        {
            var created = Issue.Create(fingerprint, signature, occurredAtUtc);
            _dbContext.Issues.Add(created);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return created;
        }

        existing.OccurrenceCount += 1;
        existing.LastSeenUtc = occurredAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public Task<Issue> TriageAsync(Guid issueId, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
        => TransitionAsync(issueId, IssueState.Triaged, occurredAtUtc, cancellationToken);

    public Task<Issue> AcknowledgeAsync(Guid issueId, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
        => TransitionAsync(issueId, IssueState.Acknowledged, occurredAtUtc, cancellationToken);

    public Task<Issue> ResolveAsync(Guid issueId, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
        => TransitionAsync(issueId, IssueState.Resolved, occurredAtUtc, cancellationToken);

    public Task<Issue> VerifyAsync(Guid issueId, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
        => TransitionAsync(issueId, IssueState.Verified, occurredAtUtc, cancellationToken);

    public Task<Issue> ReopenAsync(Guid issueId, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
        => TransitionAsync(issueId, IssueState.New, occurredAtUtc, cancellationToken);

    public async Task<Issue> TransitionAsync(Guid issueId, IssueState targetState, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        var issue = await _dbContext.Issues.FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);
        if (issue is null)
        {
            throw new InvalidOperationException($"Issue '{issueId}' was not found.");
        }

        EnsureTransitionAllowed(issue.State, targetState);

        issue.State = targetState;
        switch (targetState)
        {
            case IssueState.Triaged:
                issue.TriagedAtUtc = occurredAtUtc;
                break;
            case IssueState.Acknowledged:
                issue.AcknowledgedAtUtc = occurredAtUtc;
                break;
            case IssueState.Resolved:
                issue.ResolvedAtUtc = occurredAtUtc;
                break;
            case IssueState.Verified:
                issue.VerifiedAtUtc = occurredAtUtc;
                break;
            case IssueState.New:
                // Regression: reopen the issue and clear downstream timestamps so
                // the next pass through the lifecycle records fresh ones.
                issue.TriagedAtUtc = null;
                issue.AcknowledgedAtUtc = null;
                issue.ResolvedAtUtc = null;
                issue.VerifiedAtUtc = null;
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return issue;
    }

    private static void EnsureTransitionAllowed(IssueState fromState, IssueState toState)
    {
        if (!AllowedTransitions.TryGetValue(fromState, out var allowedTargets) || !allowedTargets.Contains(toState))
        {
            throw new InvalidIssueTransitionException(fromState, toState);
        }
    }
}
