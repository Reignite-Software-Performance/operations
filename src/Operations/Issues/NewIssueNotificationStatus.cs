namespace Operations.Issues;

/// <summary>
/// Delivery state of the new-issue notification for an <see cref="Issue"/>. The value is
/// persisted with the issue so a failed delivery can be retried instead of lost.
/// </summary>
public enum NewIssueNotificationStatus
{
    /// <summary>Not yet delivered (or delivery failed and it is awaiting retry).</summary>
    Pending = 0,

    /// <summary>A delivery attempt is in progress.</summary>
    Delivering = 1,

    /// <summary>Delivered successfully.</summary>
    Delivered = 2,
}
