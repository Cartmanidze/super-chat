using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class QdrantClient(
    HttpClient httpClient,
    IOptions<QdrantOptions> options,
    ILogger<QdrantClient> logger) : IQdrantClient
{
    private static readonly (string FieldName, string FieldSchema)[] MemoryPayloadIndexes =
    [
        ("user_id", "keyword"),
        ("chat_id", "keyword"),
        ("peer_id", "keyword"),
        ("kind", "keyword"),
        ("provider", "keyword"),
        ("transport", "keyword"),
        ("ts_from", "integer"),
        ("ts_to", "integer")
    ];

    public async Task EnsureMemoryCollectionAsync(CancellationToken cancellationToken)
    {
        var configuredOptions = options.Value;
        var collectionName = configuredOptions.MemoryCollectionName.Trim();

        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new InvalidOperationException("Qdrant memory collection name is not configured.");
        }

        if (!await CollectionExistsAsync(collectionName, cancellationToken))
        {
            await CreateMemoryCollectionAsync(configuredOptions, collectionName, cancellationToken);
        }

        foreach (var (fieldName, fieldSchema) in MemoryPayloadIndexes)
        {
            await EnsurePayloadIndexAsync(collectionName, fieldName, fieldSchema, cancellationToken);
        }
    }

    private async Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/collections/{Uri.EscapeDataString(collectionName)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    private async Task CreateMemoryCollectionAsync(
        QdrantOptions configuredOptions,
        string collectionName,
        CancellationToken cancellationToken)
    {
        var vectors = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [configuredOptions.DenseVectorName] = new
            {
                size = configuredOptions.DenseVectorSize,
                distance = "Cosine"
            }
        };

        var sparseVectors = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [configuredOptions.SparseVectorName] = new { }
        };

        var payload = new
        {
            vectors,
            sparse_vectors = sparseVectors
        };

        using var response = await httpClient.PutAsJsonAsync(
            $"/collections/{Uri.EscapeDataString(collectionName)}",
            payload,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogInformation("Qdrant collection {CollectionName} already exists.", collectionName);
            return;
        }

        response.EnsureSuccessStatusCode();
        logger.LogInformation("Created Qdrant collection {CollectionName}.", collectionName);
    }

    private async Task EnsurePayloadIndexAsync(
        string collectionName,
        string fieldName,
        string fieldSchema,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            field_name = fieldName,
            field_schema = fieldSchema
        };

        using var response = await httpClient.PutAsJsonAsync(
            $"/collections/{Uri.EscapeDataString(collectionName)}/index",
            payload,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}
