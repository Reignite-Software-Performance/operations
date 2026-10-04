namespace Operations.Issues;

/// <summary>
/// Result of an atomic create-or-get on an <see cref="Issue"/>. <see cref="Created"/> is
/// true only for the single caller that actually inserted the issue.
/// </summary>
/// <param name="Issue">The persisted issue instance (new or pre-existing).</param>
/// <param name="Created">True if this call created the issue.</param>
public sealed record IssueClaim(Issue Issue, bool Created);
