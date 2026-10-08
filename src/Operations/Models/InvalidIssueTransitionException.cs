namespace Operations.Models;

/// <summary>
/// Thrown when a caller attempts to move an <see cref="Issue"/> between two states
/// that are not an allowed transition in the lifecycle state machine.
/// </summary>
public sealed class InvalidIssueTransitionException : Exception
{
    public InvalidIssueTransitionException(IssueState fromState, IssueState toState)
        : base($"Invalid issue lifecycle transition from '{fromState}' to '{toState}'.")
    {
        FromState = fromState;
        ToState = toState;
    }

    public IssueState FromState { get; }

    public IssueState ToState { get; }
}
