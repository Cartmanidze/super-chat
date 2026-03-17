using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class QdrantClientTests
{
    [Fact]
    public async Task EnsureMemoryCollectionAsync_CreatesCollectionAndPayloadIndexes_WhenCollectionIsMissing()
    {
        var sdkClient = new RecordingQdrantSdkClient
        {
            CollectionExistsResult = false
        };

        var service = CreateService(sdkClient);

        await service.EnsureMemoryCollectionAsync(CancellationToken.None);

        var collectionExistsCall = Assert.Single(sdkClient.CollectionExistsCalls);
        Assert.Equal("memory_bgem3_v1", collectionExistsCall.CollectionName);

        var createCollectionCall = Assert.Single(sdkClient.CreateCollectionCalls);
        Assert.Equal("memory_bgem3_v1", createCollectionCall.CollectionName);
        Assert.Equal((ulong)1024, createCollectionCall.VectorsConfig.Map["text-dense"].Size);
        Assert.Equal(Distance.Cosine, createCollectionCall.VectorsConfig.Map["text-dense"].Distance);
        Assert.True(createCollectionCall.SparseVectorsConfig.Map.ContainsKey("text-sparse"));

        Assert.Collection(
            sdkClient.CreatePayloadIndexCalls,
            call => AssertPayloadIndexCall(call, "user_id", PayloadSchemaType.Keyword),
            call => AssertPayloadIndexCall(call, "chat_id", PayloadSchemaType.Keyword),
            call => AssertPayloadIndexCall(call, "peer_id", PayloadSchemaType.Keyword),
            call => AssertPayloadIndexCall(call, "kind", PayloadSchemaType.Keyword),
            call => AssertPayloadIndexCall(call, "provider", PayloadSchemaType.Keyword),
            call => AssertPayloadIndexCall(call, "transport", PayloadSchemaType.Keyword),
            call => AssertPayloadIndexCall(call, "ts_from", PayloadSchemaType.Integer),
            call => AssertPayloadIndexCall(call, "ts_to", PayloadSchemaType.Integer));
    }

    [Fact]
    public async Task EnsureMemoryCollectionAsync_SkipsCollectionCreation_WhenCollectionAlreadyExists()
    {
        var sdkClient = new RecordingQdrantSdkClient
        {
            CollectionExistsResult = true
        };

        var service = CreateService(sdkClient);

        await service.EnsureMemoryCollectionAsync(CancellationToken.None);

        Assert.Single(sdkClient.CollectionExistsCalls);
        Assert.Empty(sdkClient.CreateCollectionCalls);
        Assert.Equal(8, sdkClient.CreatePayloadIndexCalls.Count);
    }

    [Fact]
    public async Task UpsertMemoryPointsAsync_SendsNamedDenseAndSparseVectorsWithPayload()
    {
        var sdkClient = new RecordingQdrantSdkClient();
        var service = CreateService(sdkClient);

        await service.UpsertMemoryPointsAsync(
            [
                new QdrantMemoryPoint(
                    "11111111-1111-1111-1111-111111111111",
                    [0.1f, 0.2f, 0.3f],
                    new SparseTextVector([7, 11], [0.6f, 0.4f]),
                    new QdrantChunkPayload
                    {
                        UserId = "user-1",
                        Source = "telegram",
                        Provider = "telegram",
                        Transport = "matrix_bridge",
                        ChatId = "!room:matrix.localhost",
                        Kind = "dialog_chunk",
                        TsFrom = 1234567890L,
                        TsTo = 1234567999L,
                        ChunkId = "11111111-1111-1111-1111-111111111111",
                        EmbeddingVersion = "bge-m3-v1",
                        ChunkVersion = 1,
                        MessageCount = 1,
                        ContentHash = "hash-a"
                    })
            ],
            CancellationToken.None);

        var upsertCall = Assert.Single(sdkClient.UpsertCalls);
        Assert.Equal("memory_bgem3_v1", upsertCall.CollectionName);

        var point = Assert.Single(upsertCall.Points);
        Assert.Equal(PointId.PointIdOptionsOneofCase.Uuid, point.Id.PointIdOptionsCase);
        Assert.Equal("11111111-1111-1111-1111-111111111111", point.Id.Uuid);

        var denseVector = point.Vectors.Vectors_.Vectors["text-dense"];
        Assert.Equal(Vector.VectorOneofCase.Dense, denseVector.VectorCase);
        Assert.Equal([0.1f, 0.2f, 0.3f], denseVector.Dense.Data);

        var sparseVector = point.Vectors.Vectors_.Vectors["text-sparse"];
        Assert.Equal(Vector.VectorOneofCase.Sparse, sparseVector.VectorCase);
        Assert.Equal([7u, 11u], sparseVector.Sparse.Indices);
        Assert.Equal([0.6f, 0.4f], sparseVector.Sparse.Values);

        Assert.Equal("user-1", point.Payload["user_id"].StringValue);
        Assert.Equal("!room:matrix.localhost", point.Payload["chat_id"].StringValue);
        Assert.Equal("dialog_chunk", point.Payload["kind"].StringValue);
        Assert.Equal(1234567890L, point.Payload["ts_from"].IntegerValue);
    }

    [Fact]
    public async Task QueryMemoryPointsAsync_SendsHybridRrfQueryWithPayloadFilter()
    {
        var sdkClient = new RecordingQdrantSdkClient
        {
            QueryResult =
            [
                new ScoredPoint
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Score = 0.91f,
                    Payload =
                    {
                        ["chunk_id"] = "11111111-1111-1111-1111-111111111111"
                    }
                }
            ]
        };

        var service = CreateService(sdkClient);

        var result = await service.QueryMemoryPointsAsync(
            new QdrantHybridQuery(
                [0.1f, 0.2f, 0.3f],
                new SparseTextVector([7, 11], [0.6f, 0.4f]),
                "user-1",
                "!room:matrix.localhost",
                "ivan",
                "dialog_chunk",
                24,
                8),
            CancellationToken.None);

        var queryCall = Assert.Single(sdkClient.QueryCalls);
        Assert.Equal("memory_bgem3_v1", queryCall.CollectionName);
        Assert.Equal((ulong)8, queryCall.Limit);
        Assert.Equal(Query.VariantOneofCase.Rrf, queryCall.Query.VariantCase);
        Assert.Equal(WithPayloadSelector.SelectorOptionsOneofCase.Include, queryCall.PayloadSelector.SelectorOptionsCase);
        Assert.Equal(["chunk_id"], queryCall.PayloadSelector.Include.Fields);
        Assert.False(queryCall.VectorsSelector.Enable);

        Assert.Collection(
            queryCall.Prefetch,
            prefetch =>
            {
                Assert.Equal("text-dense", prefetch.Using);
                Assert.Equal((ulong)24, prefetch.Limit);
                Assert.Equal([0.1f, 0.2f, 0.3f], prefetch.Query.Nearest.Dense.Data);
                Assert.Equal(
                    ["user_id", "chat_id", "peer_id", "kind"],
                    prefetch.Filter.Must.Select(condition => condition.Field.Key).ToArray());
            },
            prefetch =>
            {
                Assert.Equal("text-sparse", prefetch.Using);
                Assert.Equal((ulong)24, prefetch.Limit);
                Assert.Equal([7u, 11u], prefetch.Query.Nearest.Sparse.Indices);
                Assert.Equal([0.6f, 0.4f], prefetch.Query.Nearest.Sparse.Values);
                Assert.Equal(
                    ["user_id", "chat_id", "peer_id", "kind"],
                    prefetch.Filter.Must.Select(condition => condition.Field.Key).ToArray());
            });

        var point = Assert.Single(result);
        Assert.Equal("11111111-1111-1111-1111-111111111111", point.PointId);
        Assert.Equal("11111111-1111-1111-1111-111111111111", point.ChunkId);
        Assert.Equal(0.91, point.Score, 3);
    }

    private static QdrantClient CreateService(RecordingQdrantSdkClient sdkClient)
    {
        var options = Options.Create(new QdrantOptions
        {
            BaseUrl = "http://localhost:6333",
            GrpcPort = 6334,
            AutoInitialize = true,
            MemoryCollectionName = "memory_bgem3_v1",
            DenseVectorName = "text-dense",
            SparseVectorName = "text-sparse",
            DenseVectorSize = 1024
        });

        return new QdrantClient(sdkClient, options, NullLogger<QdrantClient>.Instance);
    }

    private static void AssertPayloadIndexCall(
        CreatePayloadIndexCall call,
        string fieldName,
        PayloadSchemaType schemaType)
    {
        Assert.Equal("memory_bgem3_v1", call.CollectionName);
        Assert.Equal(fieldName, call.FieldName);
        Assert.Equal(schemaType, call.SchemaType);
    }

    private sealed class RecordingQdrantSdkClient : IQdrantSdkClient
    {
        public bool CollectionExistsResult { get; set; }

        public IReadOnlyList<ScoredPoint> QueryResult { get; set; } = [];

        public List<CollectionExistsCall> CollectionExistsCalls { get; } = [];

        public List<CreateCollectionCall> CreateCollectionCalls { get; } = [];

        public List<CreatePayloadIndexCall> CreatePayloadIndexCalls { get; } = [];

        public List<UpsertCall> UpsertCalls { get; } = [];

        public List<QueryCall> QueryCalls { get; } = [];

        public Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken)
        {
            CollectionExistsCalls.Add(new CollectionExistsCall(collectionName));
            return Task.FromResult(CollectionExistsResult);
        }

        public Task CreateCollectionAsync(
            string collectionName,
            VectorParamsMap vectorsConfig,
            SparseVectorConfig sparseVectorsConfig,
            CancellationToken cancellationToken)
        {
            CreateCollectionCalls.Add(new CreateCollectionCall(collectionName, vectorsConfig, sparseVectorsConfig));
            return Task.CompletedTask;
        }

        public Task CreatePayloadIndexAsync(
            string collectionName,
            string fieldName,
            PayloadSchemaType schemaType,
            CancellationToken cancellationToken)
        {
            CreatePayloadIndexCalls.Add(new CreatePayloadIndexCall(collectionName, fieldName, schemaType));
            return Task.CompletedTask;
        }

        public Task UpsertAsync(
            string collectionName,
            IReadOnlyList<PointStruct> points,
            CancellationToken cancellationToken)
        {
            UpsertCalls.Add(new UpsertCall(collectionName, points));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ScoredPoint>> QueryAsync(
            string collectionName,
            Query query,
            IReadOnlyList<PrefetchQuery> prefetch,
            ulong limit,
            WithPayloadSelector payloadSelector,
            WithVectorsSelector vectorsSelector,
            CancellationToken cancellationToken)
        {
            QueryCalls.Add(new QueryCall(collectionName, query, prefetch, limit, payloadSelector, vectorsSelector));
            return Task.FromResult(QueryResult);
        }
    }

    private sealed record CollectionExistsCall(string CollectionName);

    private sealed record CreateCollectionCall(
        string CollectionName,
        VectorParamsMap VectorsConfig,
        SparseVectorConfig SparseVectorsConfig);

    private sealed record CreatePayloadIndexCall(
        string CollectionName,
        string FieldName,
        PayloadSchemaType SchemaType);

    private sealed record UpsertCall(
        string CollectionName,
        IReadOnlyList<PointStruct> Points);

    private sealed record QueryCall(
        string CollectionName,
        Query Query,
        IReadOnlyList<PrefetchQuery> Prefetch,
        ulong Limit,
        WithPayloadSelector PayloadSelector,
        WithVectorsSelector VectorsSelector);
}
