using Microsoft.Extensions.DependencyInjection;
using Operations;
using Operations.Ingestion;
using Operations.Issues;
using Operations.Notifications;
using Operations.Tests.Fakes;
using Xunit;

namespace Operations.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddIssueTracker_RegistersNotifierAndCollaborators()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIssueTracker();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<GroupNotifier>(provider.GetRequiredService<IIssueNotifier>());
        Assert.IsType<LoggingNotificationChannel>(provider.GetRequiredService<IIssueNotificationChannel>());
        Assert.IsType<InMemoryIssueRepository>(provider.GetRequiredService<IIssueRepository>());
        Assert.IsType<IssueIngestionService>(provider.GetRequiredService<IIssueIngestionService>());
    }

    [Fact]
    public void AddIssueTracker_AllowsNotifierToBeSwapped()
    {
        var custom = new RecordingIssueNotifier();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IIssueNotifier>(custom);
        services.AddIssueTracker();

        using var provider = services.BuildServiceProvider();

        Assert.Same(custom, provider.GetRequiredService<IIssueNotifier>());
    }

    [Fact]
    public void AddIssueTracker_AllowsChannelToBeSwapped()
    {
        var channel = new RecordingNotificationChannel();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IIssueNotificationChannel>(channel);
        services.AddIssueTracker();

        using var provider = services.BuildServiceProvider();

        Assert.Same(channel, provider.GetRequiredService<IIssueNotificationChannel>());
    }

    [Fact]
    public async Task AddIssueTracker_IngestionIsResolvableThroughDI()
    {
        var channel = new RecordingNotificationChannel();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IIssueNotificationChannel>(channel);
        services.AddIssueTracker();

        using var provider = services.BuildServiceProvider();
        var ingestion = provider.GetRequiredService<IIssueIngestionService>();

        var result = await ingestion.IngestSignatureAsync(
            "build-failed:MSB1003",
            "fp-di",
            new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Utc));

        Assert.True(result.IsNew);
        Assert.Equal(1, channel.Count);
    }

    [Fact]
    public void AddIssueTracker_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddIssueTracker(null!));
    }
}
