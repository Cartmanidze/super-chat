using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SuperChat.Infrastructure.Composition;

namespace SuperChat.Worker;

public static class WorkerServiceConfiguration
{
    public static IServiceCollection AddWorkerServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSuperChatBootstrap(
            configuration,
            enablePipelineScheduling: true,
            enablePipelineConsumers: true);

        return services;
    }
}
