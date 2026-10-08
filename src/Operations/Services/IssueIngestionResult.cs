using Operations.Models;

namespace Operations.Services;

/// <summary>Whether an ingestion created a new issue or matched an already-tracked one.</summary>
public enum IngestionOutcome
{
    New,
    AlreadyTracked,
}

/// <summary>Result of ingesting a signature: the outcome plus the affected <see cref="Issue"/>.</summary>
public sealed record IssueIngestionResult(IngestionOutcome Outcome, Issue Issue)
{
    public bool IsNew => Outcome == IngestionOutcome.New;

    public bool IsAlreadyTracked => Outcome == IngestionOutcome.AlreadyTracked;

    public static IssueIngestionResult New(Issue issue) => new(IngestionOutcome.New, issue);

    public static IssueIngestionResult AlreadyTracked(Issue issue) => new(IngestionOutcome.AlreadyTracked, issue);
}
