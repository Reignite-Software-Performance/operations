using System.Collections.Concurrent;
using Operations.Notifications;

namespace Operations.Tests.Fakes;

/// <summary>Captures <see cref="NewIssueNotification"/> payloads for assertions.</summary>
public sealed class RecordingIssueNotifier : IIssueNotifier
{
    public ConcurrentQueue<NewIssueNotification> Notifications { get; } = new();

    public int Count => Notifications.Count;

    public IReadOnlyList<NewIssueNotification> All => Notifications.ToArray();

    public Task NotifyNewIssueAsync(NewIssueNotification notification, CancellationToken cancellationToken = default)
    {
        Notifications.Enqueue(notification);
        return Task.CompletedTask;
    }
}
