using System.Collections.Concurrent;

namespace Operations.Issues;

/// <summary>
/// Thread-safe in-memory <see cref="IIssueRepository"/>. Suitable for tests and
/// single-process hosts; swap for an EF Core-backed implementation in production.
/// </summary>
public sealed class InMemoryIssueRepository : IIssueRepository
{
    private readonly ConcurrentDictionary<string, Issue> _issues = new(StringComparer.Ordinal);

    public Task<Issue?> FindByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        _issues.TryGetValue(fingerprint, out var issue);
        return Task.FromResult(issue);
    }

    public Task<IssueClaim> GetOrAddAsync(Issue candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        // ConcurrentDictionary guarantees a single stored value; ReferenceEquals tells the
        // caller whether its own candidate won the race and must therefore be treated as new.
        var stored = _issues.GetOrAdd(candidate.Fingerprint, candidate);
        return Task.FromResult(new IssueClaim(stored, ReferenceEquals(stored, candidate)));
    }

    public Task UpdateAsync(Issue issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);
        _issues[issue.Fingerprint] = issue;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Issue>> ListAsync(IssueState? state = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Issue> result = _issues.Values
            .Where(issue => state is null || issue.State == state)
            .OrderBy(issue => issue.FirstSeenUtc)
            .ThenBy(issue => issue.Fingerprint, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Issue>> ListPendingNewIssueNotificationsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Issue> result = _issues.Values
            .Where(issue => !issue.IsNotificationDelivered)
            .OrderBy(issue => issue.FirstSeenUtc)
            .ThenBy(issue => issue.Fingerprint, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(result);
    }
}
