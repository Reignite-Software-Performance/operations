using Operations.Issues;

namespace Operations.Ingestion;

/// <summary>Result of an ingestion call.</summary>
/// <param name="Outcome">Whether the fingerprint was new or already tracked.</param>
/// <param name="Issue">The affected issue (created or updated).</param>
public sealed record IngestionResult(IngestionOutcome Outcome, Issue Issue)
{
    /// <summary>True when this ingestion created a new issue.</summary>
    public bool IsNew => Outcome == IngestionOutcome.New;
}
