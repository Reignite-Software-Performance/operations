namespace Operations.Models;

/// <summary>
/// Lifecycle state of an <see cref="Issue"/>: new → triaged → acknowledged → resolved → verified.
/// </summary>
public enum IssueState
{
    New = 0,
    Triaged = 1,
    Acknowledged = 2,
    Resolved = 3,
    Verified = 4,
}
