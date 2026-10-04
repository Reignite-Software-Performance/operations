namespace Operations.Issues;

/// <summary>
/// Lifecycle states an <see cref="Issue"/> can occupy.
/// </summary>
public enum IssueState
{
    New,
    Triaged,
    Acknowledged,
    Resolved,
    Verified,
}
