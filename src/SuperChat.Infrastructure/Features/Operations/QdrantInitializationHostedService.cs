using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class QdrantInitializationHostedService(
    IQdrantClient qdrantClient,
    IOptions<QdrantOptions> qdrantOptions,
    ILogger<QdrantInitializationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = qdrantOptions.Value;

        if (!options.AutoInitialize)
        {
            logger.LogInformation("Qdrant auto-initialization is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            logger.LogWarning("Qdrant base URL is empty. Skipping initialization.");
            return;
        }

        try
        {
            await qdrantClient.EnsureMemoryCollectionAsync(cancellationToken);
            logger.LogInformation(
                "Qdrant collection {CollectionName} is ready.",
                options.MemoryCollectionName);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Qdrant initialization failed for {BaseUrl}. Startup will continue without retrieval indexing bootstrap.",
                options.BaseUrl);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
