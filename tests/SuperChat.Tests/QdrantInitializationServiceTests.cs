using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Intelligence.Retrieval;

namespace SuperChat.Tests;

public sealed class QdrantInitializationServiceTests
{
    [Fact]
    public async Task InitializeAsync_SkipsWhenAutoInitializationDisabled()
    {
        var qdrantClient = new RecordingQdrantClient();
        var service = CreateService(
            qdrantClient,
            new QdrantOptions
            {
                AutoInitialize = false,
                BaseUrl = "http://qdrant:6333",
                MemoryCollectionName = "memory_bgem3_v1"
            });

        await service.InitializeAsync(CancellationToken.None);

        Assert.Equal(0, qdrantClient.EnsureMemoryCollectionCalls);
    }

    [Fact]
    public async Task InitializeAsync_SkipsWhenBaseUrlIsEmpty()
    {
        var qdrantClient = new RecordingQdrantClient();
        var service = CreateService(
            qdrantClient,
            new QdrantOptions
            {
                AutoInitialize = true,
                BaseUrl = " ",
                MemoryCollectionName = "memory_bgem3_v1"
            });

        await service.InitializeAsync(CancellationToken.None);

        Assert.Equal(0, qdrantClient.EnsureMemoryCollectionCalls);
    }

    [Fact]
    public async Task InitializeAsync_EnsuresMemoryCollectionWhenEnabled()
    {
        var qdrantClient = new RecordingQdrantClient();
        var service = CreateService(
            qdrantClient,
            new QdrantOptions
            {
                AutoInitialize = true,
                BaseUrl = "http://qdrant:6333",
                MemoryCollectionName = "memory_bgem3_v1"
            });

        await service.InitializeAsync(CancellationToken.None);

        Assert.Equal(1, qdrantClient.EnsureMemoryCollectionCalls);
    }

    private static QdrantInitializationService CreateService(
        IQdrantClient qdrantClient,
        QdrantOptions options)
    {
        return new QdrantInitializationService(
            qdrantClient,
            Options.Create(options),
            NullLogger<QdrantInitializationService>.Instance);
    }

    private sealed class RecordingQdrantClient : IQdrantClient
    {
        public int EnsureMemoryCollectionCalls { get; private set; }

        public Task EnsureMemoryCollectionAsync(CancellationToken cancellationToken)
        {
            EnsureMemoryCollectionCalls++;
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
