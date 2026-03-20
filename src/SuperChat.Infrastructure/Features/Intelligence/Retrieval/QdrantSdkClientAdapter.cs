using Qdrant.Client.Grpc;
using QdrantSdk = Qdrant.Client.QdrantClient;

namespace SuperChat.Infrastructure.Features.Intelligence.Retrieval;

public interface IQdrantSdkClient
{
    Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken);

    Task CreateCollectionAsync(
        string collectionName,
        VectorParamsMap vectorsConfig,
        SparseVectorConfig sparseVectorsConfig,
        CancellationToken cancellationToken);

    Task CreatePayloadIndexAsync(
        string collectionName,
        string fieldName,
        PayloadSchemaType schemaType,
        CancellationToken cancellationToken);

    Task UpsertAsync(
        string collectionName,
        IReadOnlyList<PointStruct> points,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoredPoint>> QueryAsync(
        string collectionName,
        Query query,
        IReadOnlyList<PrefetchQuery> prefetch,
        ulong limit,
        WithPayloadSelector payloadSelector,
        WithVectorsSelector vectorsSelector,
        CancellationToken cancellationToken);
}

public sealed class QdrantSdkClientAdapter(QdrantSdk client) : IQdrantSdkClient
{
    public Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken)
    {
        return client.CollectionExistsAsync(collectionName, cancellationToken);
    }

    public Task CreateCollectionAsync(
        string collectionName,
        VectorParamsMap vectorsConfig,
        SparseVectorConfig sparseVectorsConfig,
        CancellationToken cancellationToken)
    {
        return client.CreateCollectionAsync(
            collectionName,
            vectorsConfig,
            sparseVectorsConfig: sparseVectorsConfig,
            cancellationToken: cancellationToken);
    }

    public async Task CreatePayloadIndexAsync(
        string collectionName,
        string fieldName,
        PayloadSchemaType schemaType,
        CancellationToken cancellationToken)
    {
        await client.CreatePayloadIndexAsync(
            collectionName,
            fieldName,
            schemaType,
            indexParams: null,
            wait: true,
            ordering: null,
            cancellationToken: cancellationToken);
    }

    public async Task UpsertAsync(
        string collectionName,
        IReadOnlyList<PointStruct> points,
        CancellationToken cancellationToken)
    {
        await client.UpsertAsync(
            collectionName,
            points,
            wait: true,
            ordering: null,
            shardKeySelector: null,
            cancellationToken: cancellationToken);
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
        return client.QueryAsync(
            collectionName,
            query,
            prefetch,
            usingVector: string.Empty,
            filter: null,
            scoreThreshold: null,
            searchParams: null,
            limit,
            offset: 0,
            payloadSelector,
            vectorsSelector,
            readConsistency: null,
            shardKeySelector: null,
            lookupFrom: null,
            timeout: null,
            cancellationToken: cancellationToken);
    }
}
