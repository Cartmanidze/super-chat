using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class QdrantInitializationService(
    IQdrantClient qdrantClient,
    IOptions<QdrantOptions> qdrantOptions,
    ILogger<QdrantInitializationService> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
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

        await qdrantClient.EnsureMemoryCollectionAsync(cancellationToken);
        logger.LogInformation(
            "Qdrant collection {CollectionName} is ready.",
            options.MemoryCollectionName);
    }
}
