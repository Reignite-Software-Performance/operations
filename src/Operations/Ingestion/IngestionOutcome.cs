namespace Operations.Ingestion;

/// <summary>Outcome of ingesting a signature.</summary>
public enum IngestionOutcome
{
    /// <summary>A new issue was created for a previously unseen fingerprint.</summary>
    New,

    /// <summary>An existing issue was found and its occurrence count incremented.</summary>
    AlreadyTracked,
}
