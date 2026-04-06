using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
}
