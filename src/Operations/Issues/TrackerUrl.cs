namespace Operations.Issues;

/// <summary>
/// Builds the deterministic tracker route for an issue. The URL depends only on the
/// fingerprint, so the same fingerprint always yields the same link.
/// </summary>
public static class TrackerUrl
{
    public const string BasePath = "/tracker";

    /// <summary>Returns <c>/tracker/{fingerprint}</c> with the fingerprint path-escaped.</summary>
    public static string ForFingerprint(string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        return $"{BasePath}/{Uri.EscapeDataString(fingerprint)}";
    }
}
