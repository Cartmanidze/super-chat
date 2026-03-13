using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        var payload = new
        {
            points = points.Select(point => new
            {
                id = point.PointId,
                vector = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [configuredOptions.DenseVectorName] = point.DenseVector,
                    [configuredOptions.SparseVectorName] = new
                    {
                        indices = point.SparseVector.Indices,
                        values = point.SparseVector.Values
                    }
                },
                payload = point.Payload
            })
        };

        using var response = await httpClient.PutAsJsonAsync(
            $"/collections/{Uri.EscapeDataString(collectionName)}/points?wait=true",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<QdrantQueryPoint>> QueryMemoryPointsAsync(QdrantHybridQuery request, CancellationToken cancellationToken)
    {
        var configuredOptions = options.Value;
        var collectionName = configuredOptions.MemoryCollectionName.Trim();

        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new InvalidOperationException("Qdrant memory collection name is not configured.");
        }

        var filter = BuildFilter(request);
        var payload = new HybridQueryRequestDto(
            [
                new DensePrefetchDto(
                    request.DenseVector,
                    configuredOptions.DenseVectorName,
                    filter,
                    request.PrefetchLimit),
                new SparsePrefetchDto(
                    new SparseVectorDto(request.SparseVector.Indices, request.SparseVector.Values),
                    configuredOptions.SparseVectorName,
                    filter,
                    request.PrefetchLimit)
            ],
            new FusionQueryDto("rrf"),
            request.Limit,
            true,
            false);

        var jsonPayload = JsonSerializer.Serialize(payload);
        using var content = new StringContent(jsonPayload);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using var response = await httpClient.PostAsync(
            $"/collections/{Uri.EscapeDataString(collectionName)}/points/query",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var pointsElement = ResolvePointsElement(document.RootElement);
        if (pointsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var points = new List<QdrantQueryPoint>();
        foreach (var pointElement in pointsElement.EnumerateArray())
        {
            var pointId = pointElement.TryGetProperty("id", out var idElement)
                ? idElement.ValueKind == JsonValueKind.String
                    ? idElement.GetString() ?? string.Empty
                    : idElement.GetRawText()
                : string.Empty;

            var score = pointElement.TryGetProperty("score", out var scoreElement) &&
                        scoreElement.ValueKind is JsonValueKind.Number
                ? scoreElement.GetDouble()
                : 0d;

            var parsedPayload = pointElement.TryGetProperty("payload", out var payloadElement)
                ? ConvertPayloadObject(payloadElement)
                : new Dictionary<string, object?>(StringComparer.Ordinal);

            points.Add(new QdrantQueryPoint(pointId, score, parsedPayload));
        }

        return points;
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

    private static FilterDto BuildFilter(QdrantHybridQuery request)
    {
        var must = new List<FieldMatchDto>
        {
            new("user_id", new MatchValueDto(request.UserId))
        };

        if (!string.IsNullOrWhiteSpace(request.ChatId))
        {
            must.Add(new FieldMatchDto("chat_id", new MatchValueDto(request.ChatId)));
        }

        if (!string.IsNullOrWhiteSpace(request.PeerId))
        {
            must.Add(new FieldMatchDto("peer_id", new MatchValueDto(request.PeerId)));
        }

        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            must.Add(new FieldMatchDto("kind", new MatchValueDto(request.Kind)));
        }

        return new FilterDto(must);
    }

    private static JsonElement ResolvePointsElement(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("result", out var resultElement))
        {
            return default;
        }

        if (resultElement.ValueKind == JsonValueKind.Array)
        {
            return resultElement;
        }

        return resultElement.TryGetProperty("points", out var pointsElement)
            ? pointsElement
            : default;
    }

    private static Dictionary<string, object?> ConvertPayloadObject(JsonElement payloadElement)
    {
        if (payloadElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in payloadElement.EnumerateObject())
        {
            payload[property.Name] = ConvertJsonValue(property.Value);
        }

        return payload;
    }

    private static object? ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToList(),
            JsonValueKind.Object => ConvertPayloadObject(element),
            _ => element.GetRawText()
        };
    }

    private sealed record HybridQueryRequestDto(
        [property: JsonPropertyName("prefetch")] IReadOnlyList<object> Prefetch,
        [property: JsonPropertyName("query")] FusionQueryDto Query,
        [property: JsonPropertyName("limit")] int Limit,
        [property: JsonPropertyName("with_payload")] bool WithPayload,
        [property: JsonPropertyName("with_vector")] bool WithVector);

    private sealed record DensePrefetchDto(
        [property: JsonPropertyName("query")] IReadOnlyList<float> Query,
        [property: JsonPropertyName("using")] string Using,
        [property: JsonPropertyName("filter")] FilterDto Filter,
        [property: JsonPropertyName("limit")] int Limit);

    private sealed record SparsePrefetchDto(
        [property: JsonPropertyName("query")] SparseVectorDto Query,
        [property: JsonPropertyName("using")] string Using,
        [property: JsonPropertyName("filter")] FilterDto Filter,
        [property: JsonPropertyName("limit")] int Limit);

    private sealed record SparseVectorDto(
        [property: JsonPropertyName("indices")] IReadOnlyList<int> Indices,
        [property: JsonPropertyName("values")] IReadOnlyList<float> Values);

    private sealed record FusionQueryDto(
        [property: JsonPropertyName("fusion")] string Fusion);

    private sealed record FilterDto(
        [property: JsonPropertyName("must")] IReadOnlyList<FieldMatchDto> Must);

    private sealed record FieldMatchDto(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("match")] MatchValueDto Match);

    private sealed record MatchValueDto(
        [property: JsonPropertyName("value")] string Value);
}
