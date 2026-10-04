using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Operations.Ingestion;
using Operations.Issues;
using Operations.Notifications;

namespace Operations;

/// <summary>
/// DI registration for the issue tracker. Registers the notifier through
/// <see cref="IIssueNotifier"/> so a real transport can be swapped in.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ingestion, persistence and notification services. Uses
    /// <c>TryAdd</c> so callers can override any registration (e.g. replace the logging
    /// notification channel with an email/webhook channel).
    /// </summary>
    public static IServiceCollection AddIssueTracker(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIssueRepository, InMemoryIssueRepository>();
        services.TryAddSingleton<IIssueNotificationChannel, LoggingNotificationChannel>();
        services.TryAddSingleton<IIssueNotifier, GroupNotifier>();
        services.TryAddSingleton<IIssueIngestionService, IssueIngestionService>();

        return services;
    }
}
