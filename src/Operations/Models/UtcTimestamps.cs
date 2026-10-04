namespace Operations.Models;

/// <summary>
/// Normalises incoming timestamps to UTC so persisted values are comparable
/// regardless of the <see cref="DateTime.Kind"/> supplied by callers.
/// </summary>
internal static class UtcTimestamps
{
    public static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        // Unspecified is treated as already-UTC: ingestion timestamps are UTC by contract.
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
