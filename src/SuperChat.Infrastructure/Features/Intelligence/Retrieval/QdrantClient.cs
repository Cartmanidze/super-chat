using System.Diagnostics;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Diagnostics;

namespace SuperChat.Infrastructure.Features.Intelligence.Retrieval;

public sealed class QdrantClient(
    IQdrantSdkClient sdkClient,
    IOptions<QdrantOptions> options,
    ILogger<QdrantClient> logger) : IQdrantClient
{
    private static readonly (string FieldName, PayloadSchemaType SchemaType)[] MemoryPayloadIndexes =
    [
        ("user_id", PayloadSchemaType.Keyword),
        ("chat_id", PayloadSchemaType.Keyword),
        ("peer_id", PayloadSchemaType.Keyword),
        ("kind", PayloadSchemaType.Keyword),
        ("provider", PayloadSchemaType.Keyword),
        ("transport", PayloadSchemaType.Keyword),
        ("ts_from", PayloadSchemaType.Integer),
        ("ts_to", PayloadSchemaType.Integer)
    ];

    public async Task EnsureMemoryCollectionAsync(CancellationToken cancellationToken)
    {
        var configuredOptions = options.Value;
        var collectionName = configuredOptions.MemoryCollectionName.Trim();

        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new InvalidOperationException("Qdrant memory collection name is not configured.");
        }

        if (!await sdkClient.CollectionExistsAsync(collectionName, cancellationToken))
        {
            await CreateMemoryCollectionAsync(configuredOptions, collectionName, cancellationToken);
        }

        foreach (var (fieldName, schemaType) in MemoryPayloadIndexes)
        {
            await EnsurePayloadIndexAsync(collectionName, fieldName, schemaType, cancellationToken);
        }
    }

    public async Task UpsertMemoryPointsAsync(IReadOnlyList<QdrantMemoryPoint> points, CancellationToken cancellationToken)
    {
        if (points.Count == 0)
        {
            return;
        }

        var configuredOptions = options.Value;
        var collectionName = configuredOptions.MemoryCollectionName.Trim();

        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new InvalidOperationException("Qdrant memory collection name is not configured.");
        }

        var stopwatch = Stopwatch.StartNew();
        AiPipelineLog.QdrantUpsertStarted(logger, collectionName, points.Count);

        try
        {
            var mappedPoints = points
                .Select(point => point.ToPointStruct(configuredOptions))
                .ToList();

            await sdkClient.UpsertAsync(collectionName, mappedPoints, cancellationToken);

            stopwatch.Stop();
            AiPipelineLog.QdrantUpsertCompleted(logger, collectionName, points.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            AiPipelineLog.QdrantUpsertFailed(logger, collectionName, points.Count, stopwatch.ElapsedMilliseconds, exception);
            throw;
        }
    }

    public async Task<IReadOnlyList<QdrantQueryPoint>> QueryMemoryPointsAsync(QdrantHybridQuery request, CancellationToken cancellationToken)
    {
        var configuredOptions = options.Value;
        var collectionName = configuredOptions.MemoryCollectionName.Trim();

        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new InvalidOperationException("Qdrant memory collection name is not configured.");
        }

        var stopwatch = Stopwatch.StartNew();
        AiPipelineLog.QdrantQueryStarted(logger, collectionName, request.Limit, request.PrefetchLimit);

        try
        {
            var filter = request.ToQdrantFilter();
            var prefetch = new List<PrefetchQuery>
            {
                new()
                {
                    Query = request.DenseVector.ToArray(),
                    Using = configuredOptions.DenseVectorName,
                    Filter = filter,
                    Limit = checked((ulong)request.PrefetchLimit)
                },
                new()
                {
                    Query = (
                        request.SparseVector.Values.ToArray(),
                        request.SparseVector.Indices.Select(index => checked((uint)index)).ToArray()),
                    Using = configuredOptions.SparseVectorName,
                    Filter = filter,
                    Limit = checked((ulong)request.PrefetchLimit)
                }
            };

            var points = await sdkClient.QueryAsync(
                collectionName,
                new Rrf(),
                prefetch,
                checked((ulong)request.Limit),
                payloadSelector: new[] { "chunk_id" },
                vectorsSelector: false,
                cancellationToken);

            var mappedPoints = points
                .Select(point => point.ToQdrantQueryPoint())
                .ToList();

            stopwatch.Stop();
            AiPipelineLog.QdrantQueryCompleted(logger, collectionName, mappedPoints.Count, stopwatch.ElapsedMilliseconds);
            return mappedPoints;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            AiPipelineLog.QdrantQueryFailed(
                logger,
                collectionName,
                request.Limit,
                request.PrefetchLimit,
                stopwatch.ElapsedMilliseconds,
                exception);
            throw;
        }
    }

    private async Task CreateMemoryCollectionAsync(
        QdrantOptions configuredOptions,
        string collectionName,
        CancellationToken cancellationToken)
    {
        var vectors = new VectorParamsMap();
        vectors.Map[configuredOptions.DenseVectorName] = new VectorParams
        {
            Size = checked((ulong)configuredOptions.DenseVectorSize),
            Distance = Distance.Cosine
        };

        var sparseVectors = new SparseVectorConfig();
        sparseVectors.Map[configuredOptions.SparseVectorName] = new SparseVectorParams();

        try
        {
            await sdkClient.CreateCollectionAsync(collectionName, vectors, sparseVectors, cancellationToken);
            logger.LogInformation("Created Qdrant collection {CollectionName}.", collectionName);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.AlreadyExists)
        {
            logger.LogInformation("Qdrant collection {CollectionName} already exists.", collectionName);
        }
    }

    private async Task EnsurePayloadIndexAsync(
        string collectionName,
        string fieldName,
        PayloadSchemaType schemaType,
        CancellationToken cancellationToken)
    {
        try
        {
            await sdkClient.CreatePayloadIndexAsync(collectionName, fieldName, schemaType, cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.AlreadyExists)
        {
        }
    }

}
