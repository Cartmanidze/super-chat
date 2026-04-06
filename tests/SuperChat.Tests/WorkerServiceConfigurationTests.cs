using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Operations;
using SuperChat.Worker;

namespace SuperChat.Tests;

public sealed class WorkerServiceConfigurationTests
{
    [Fact]
    public void AddWorkerServices_RegistersMatrixSyncBackgroundService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SuperChatDb"] = "Data Source=worker-tests.db",
                ["Persistence:Provider"] = "Sqlite",
                ["PipelineMessaging:Enabled"] = "false",
                ["SuperChat:DevSeedSampleData"] = "true"
            })
            .Build();

        WorkerServiceConfiguration.AddWorkerServices(services, configuration);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                          descriptor.ImplementationType == typeof(MatrixSyncBackgroundService));
    }

    [Fact]
    public void AddWorkerServices_WithPipelineMessagingEnabled_RegistersRebusPipelineScheduler()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SuperChatDb"] = "Host=localhost;Port=5432;Database=worker_tests;Username=test;Password=test",
                ["Persistence:Provider"] = "Postgres",
                ["PipelineMessaging:Enabled"] = "true",
                ["SuperChat:DevSeedSampleData"] = "true"
            })
            .Build();

        WorkerServiceConfiguration.AddWorkerServices(services, configuration);

        var scheduler = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPipelineCommandScheduler));
        Assert.Equal(typeof(RebusPipelineCommandScheduler), scheduler.ImplementationType);
    }
}
