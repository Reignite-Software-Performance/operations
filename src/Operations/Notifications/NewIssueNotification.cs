namespace Operations.Notifications;

/// <summary>
/// Structured payload describing a newly-created issue. Carries the four data points the
/// team needs: the signature, current occurrence count, onset time and tracker link.
/// </summary>
/// <param name="Signature">The human-readable signature text.</param>
/// <param name="OccurrenceCount">Occurrence count at the moment the issue was created.</param>
/// <param name="FirstSeenUtc">UTC onset time of the issue.</param>
/// <param name="Fingerprint">Stable fingerprint of the issue.</param>
/// <param name="TrackerUrl">Deterministic tracker link, <c>/tracker/{fingerprint}</c>.</param>
public sealed record NewIssueNotification(
    string Signature,
    int OccurrenceCount,
    DateTime FirstSeenUtc,
    string Fingerprint,
    string TrackerUrl);
