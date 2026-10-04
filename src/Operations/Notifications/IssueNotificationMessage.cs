namespace Operations.Notifications;

/// <summary>
/// A rendered notification: a subject/body for humans plus the structured payload a
/// machine channel can forward.
/// </summary>
/// <param name="Subject">Short human-readable subject line.</param>
/// <param name="Body">Human-readable body containing signature, count, onset and link.</param>
/// <param name="Payload">The structured <see cref="NewIssueNotification"/>.</param>
public sealed record IssueNotificationMessage(
    string Subject,
    string Body,
    NewIssueNotification Payload);
