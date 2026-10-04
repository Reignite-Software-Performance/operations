namespace Operations.Issues;

/// <summary>
/// Persistence seam for <see cref="Issue"/> aggregates. Implementations may back this
/// with EF Core, a document store, etc.; the in-memory implementation is the default.
/// </summary>
public interface IIssueRepository
{
    /// <summary>Returns the issue matching <paramref name="fingerprint"/>, or null.</summary>
    Task<Issue?> FindByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically returns the issue for <paramref name="candidate"/>'s fingerprint, creating
    /// it from <paramref name="candidate"/> if absent. Exactly one concurrent caller observes
    /// <see cref="IssueClaim.Created"/> as true; the rest receive the stored instance.
    /// </summary>
    Task<IssueClaim> GetOrAddAsync(Issue candidate, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing issue.</summary>
    Task UpdateAsync(Issue issue, CancellationToken cancellationToken = default);

    /// <summary>Lists issues, optionally filtered by lifecycle state.</summary>
    Task<IReadOnlyList<Issue>> ListAsync(IssueState? state = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists issues whose new-issue notification has not yet been delivered, including any
    /// stale in-progress claims that need recovery. This is the outbox read used for retries.
    /// </summary>
    Task<IReadOnlyList<Issue>> ListPendingNewIssueNotificationsAsync(CancellationToken cancellationToken = default);
}
