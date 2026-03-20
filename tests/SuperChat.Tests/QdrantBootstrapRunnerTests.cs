using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.DbMigrator;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class QdrantBootstrapRunnerTests
{
    [Fact]
    public async Task EnsureInitializedAsync_RetriesTransientFailuresAndEventuallySucceeds()
    {
        var qdrantClient = new FlakyQdrantClient(failuresBeforeSuccess: 2);
        var services = CreateServiceProvider(qdrantClient);

        await QdrantBootstrapRunner.EnsureInitializedAsync(
            services,
            CreateOptions(),
            NullLogger.Instance,
            CancellationToken.None,
            maxAttempts: 3,
            delay: TimeSpan.Zero);

        Assert.Equal(3, qdrantClient.Attempts);
    }

    [Fact]
    public async Task EnsureInitializedAsync_ThrowsWhenAllAttemptsFail()
    {
        var qdrantClient = new FlakyQdrantClient(failuresBeforeSuccess: int.MaxValue);
        var services = CreateServiceProvider(qdrantClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => QdrantBootstrapRunner.EnsureInitializedAsync(
            services,
            CreateOptions(),
            NullLogger.Instance,
            CancellationToken.None,
            maxAttempts: 3,
            delay: TimeSpan.Zero));

        Assert.Equal("Qdrant unavailable", exception.Message);
        Assert.Equal(3, qdrantClient.Attempts);
    }

    private static IServiceProvider CreateServiceProvider(IQdrantClient qdrantClient)
    {
        return new ServiceCollection()
            .AddSingleton(qdrantClient)
            .AddSingleton(Options.Create(CreateOptions()))
            .AddSingleton<ILogger<QdrantInitializationService>>(NullLogger<QdrantInitializationService>.Instance)
            .AddSingleton<QdrantInitializationService>()
            .BuildServiceProvider();
    }

    private static QdrantOptions CreateOptions()
    {
        return new QdrantOptions
        {
            AutoInitialize = true,
            BaseUrl = "http://qdrant:6333",
            MemoryCollectionName = "memory_bgem3_v1"
        };
    }

    private sealed class FlakyQdrantClient(int failuresBeforeSuccess) : IQdrantClient
    {
        private int _remainingFailures = failuresBeforeSuccess;

        public int Attempts { get; private set; }

        public Task EnsureMemoryCollectionAsync(CancellationToken cancellationToken)
        {
            Attempts++;

            if (_remainingFailures > 0)
            {
                _remainingFailures--;
                throw new InvalidOperationException("Qdrant unavailable");
            }

            return Task.CompletedTask;
        }

        public Task UpsertMemoryPointsAsync(IReadOnlyList<QdrantMemoryPoint> points, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<QdrantQueryPoint>> QueryMemoryPointsAsync(QdrantHybridQuery request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
