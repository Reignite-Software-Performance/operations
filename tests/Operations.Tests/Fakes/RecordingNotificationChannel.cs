using System.Collections.Concurrent;
using Operations.Notifications;

namespace Operations.Tests.Fakes;

/// <summary>Captures rendered notification messages for end-to-end assertions.</summary>
public sealed class RecordingNotificationChannel : IIssueNotificationChannel
{
    public ConcurrentQueue<IssueNotificationMessage> Messages { get; } = new();

    public int Count => Messages.Count;

    public IReadOnlyList<IssueNotificationMessage> All => Messages.ToArray();

    public Task SendAsync(IssueNotificationMessage message, CancellationToken cancellationToken = default)
    {
        Messages.Enqueue(message);
        return Task.CompletedTask;
    }
}
