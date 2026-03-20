using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Services;

namespace SuperChat.DbMigrator;

public static class QdrantBootstrapRunner
{
    public static async Task EnsureInitializedAsync(
        IServiceProvider serviceProvider,
        QdrantOptions qdrantOptions,
        ILogger logger,
        CancellationToken cancellationToken,
        int maxAttempts = 10,
        TimeSpan? delay = null)
    {
        if (!qdrantOptions.AutoInitialize)
        {
            logger.LogInformation("Qdrant auto-initialization is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(qdrantOptions.BaseUrl))
        {
            logger.LogWarning("Qdrant base URL is empty. Skipping initialization.");
            return;
        }

        var qdrantInitializationService = serviceProvider.GetRequiredService<QdrantInitializationService>();
        var attempts = Math.Max(1, maxAttempts);
        var retryDelay = delay ?? TimeSpan.FromSeconds(3);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await qdrantInitializationService.InitializeAsync(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (attempt < attempts)
            {
                logger.LogWarning(
                    exception,
                    "Qdrant initialization attempt {Attempt} of {MaxAttempts} failed. Retrying in {DelaySeconds} seconds.",
                    attempt,
                    attempts,
                    retryDelay.TotalSeconds);

                if (retryDelay > TimeSpan.Zero)
                {
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Qdrant initialization failed for {BaseUrl} after {AttemptCount} attempts. Deploy will stop.",
                    qdrantOptions.BaseUrl,
                    attempts);
                throw;
            }
        }
    }
}
