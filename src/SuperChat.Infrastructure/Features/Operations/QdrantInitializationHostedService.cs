using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class QdrantInitializationHostedService(
    IQdrantClient qdrantClient,
    IOptions<QdrantOptions> qdrantOptions,
    IWorkerRuntimeMonitor workerRuntimeMonitor,
    ILogger<QdrantInitializationHostedService> logger) : IHostedService
{
    private const string WorkerKey = "qdrant-initialization";
    private const string WorkerDisplayName = "Qdrant Initialization";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        workerRuntimeMonitor.RegisterWorker(WorkerKey, WorkerDisplayName);
        var options = qdrantOptions.Value;

        if (!options.AutoInitialize)
        {
            logger.LogInformation("Qdrant auto-initialization is disabled.");
            workerRuntimeMonitor.MarkDisabled(WorkerKey, WorkerDisplayName, "Qdrant auto-initialization is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            logger.LogWarning("Qdrant base URL is empty. Skipping initialization.");
            workerRuntimeMonitor.MarkDisabled(WorkerKey, WorkerDisplayName, "Qdrant base URL is empty.");
            return;
        }

        try
        {
            workerRuntimeMonitor.MarkRunning(WorkerKey, WorkerDisplayName);
            await qdrantClient.EnsureMemoryCollectionAsync(cancellationToken);
            workerRuntimeMonitor.MarkSucceeded(WorkerKey, WorkerDisplayName, $"Collection={options.MemoryCollectionName}");
            logger.LogInformation(
                "Qdrant collection {CollectionName} is ready.",
                options.MemoryCollectionName);
        }
        catch (Exception exception)
        {
            workerRuntimeMonitor.MarkFailed(WorkerKey, WorkerDisplayName, exception, $"BaseUrl={options.BaseUrl}");
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
