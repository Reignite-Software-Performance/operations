using Operations.Notifications;

namespace Operations.Tests.Fakes;

/// <summary>
/// Test notifier that can be forced to fail a configurable number of times before
/// succeeding, used to exercise the durable retry (outbox) path.
/// </summary>
public sealed class ControllableIssueNotifier : IIssueNotifier
{
    private readonly object _gate = new();
    private readonly List<NewIssueNotification> _delivered = new();
    private int _failuresRemaining;

    public ControllableIssueNotifier(int failuresBeforeSuccess = 0, bool failAlways = false)
    {
        _failuresRemaining = failuresBeforeSuccess;
        FailAlways = failAlways;
    }

    public bool FailAlways { get; set; }

    public int Attempts { get; private set; }

    public IReadOnlyList<NewIssueNotification> Delivered
    {
        get
        {
            lock (_gate)
            {
                return _delivered.ToArray();
            }
        }
    }

    public Task NotifyNewIssueAsync(NewIssueNotification notification, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            Attempts++;
            if (FailAlways || _failuresRemaining > 0)
            {
                _failuresRemaining--;
                throw new InvalidOperationException("Simulated transient notification failure.");
            }

            _delivered.Add(notification);
        }

        return Task.CompletedTask;
    }
}
