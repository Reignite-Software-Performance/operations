namespace Operations.Core.Entities;

/// <summary>
/// Represents the lifecycle state of an <see cref="Issue"/>.
/// </summary>
public enum IssueState
{
    New = 0,
    Triaged = 1,
    Acknowledged = 2,
    Resolved = 3,
    Verified = 4
}
