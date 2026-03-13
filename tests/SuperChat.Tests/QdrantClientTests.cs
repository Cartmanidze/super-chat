using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class QdrantClientTests
{
    [Fact]
    public async Task EnsureMemoryCollectionAsync_CreatesCollectionAndPayloadIndexes_WhenCollectionIsMissing()
    {
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var service = CreateService(handler);

        await service.EnsureMemoryCollectionAsync(CancellationToken.None);

        Assert.Collection(
            handler.Requests,
            request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("/collections/memory_bgem3_v1", request.Path);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.Equal("/collections/memory_bgem3_v1", request.Path);
                Assert.Contains("\"text-dense\"", request.Body, StringComparison.Ordinal);
                Assert.Contains("\"size\":1024", request.Body, StringComparison.Ordinal);
                Assert.Contains("\"text-sparse\"", request.Body, StringComparison.Ordinal);
            },
            request => AssertPayloadIndexRequest(request, "user_id", "keyword"),
            request => AssertPayloadIndexRequest(request, "chat_id", "keyword"),
            request => AssertPayloadIndexRequest(request, "peer_id", "keyword"),
            request => AssertPayloadIndexRequest(request, "kind", "keyword"),
            request => AssertPayloadIndexRequest(request, "provider", "keyword"),
            request => AssertPayloadIndexRequest(request, "transport", "keyword"),
            request => AssertPayloadIndexRequest(request, "ts_from", "integer"),
            request => AssertPayloadIndexRequest(request, "ts_to", "integer"));
    }

    [Fact]
    public async Task EnsureMemoryCollectionAsync_SkipsCollectionCreation_WhenCollectionAlreadyExists()
    {
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var service = CreateService(handler);

        await service.EnsureMemoryCollectionAsync(CancellationToken.None);

        Assert.Equal(9, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.DoesNotContain(
            handler.Requests.Skip(1),
            request => request.Method == HttpMethod.Put && request.Path == "/collections/memory_bgem3_v1");
        Assert.All(
            handler.Requests.Skip(1),
            request => Assert.Equal("/collections/memory_bgem3_v1/index", request.Path));
    }

    [Fact]
    public async Task UpsertMemoryPointsAsync_SendsNamedDenseAndSparseVectorsWithPayload()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateService(handler);

        await service.UpsertMemoryPointsAsync(
            [
                new QdrantMemoryPoint(
                    "11111111-1111-1111-1111-111111111111",
                    [0.1f, 0.2f, 0.3f],
                    new SparseTextVector([7, 11], [0.6f, 0.4f]),
                    new Dictionary<string, object?>
                    {
                        ["user_id"] = "user-1",
                        ["chat_id"] = "!room:matrix.localhost",
                        ["kind"] = "dialog_chunk",
                        ["ts_from"] = 1234567890L
                    })
            ],
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/collections/memory_bgem3_v1/points", request.Path);
        Assert.Equal("?wait=true", request.Query);
        Assert.Contains("\"id\":\"11111111-1111-1111-1111-111111111111\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"text-dense\":[0.1,0.2,0.3]", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"text-sparse\":{\"indices\":[7,11],\"values\":[0.6,0.4]}", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"user_id\":\"user-1\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"chat_id\":\"!room:matrix.localhost\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"dialog_chunk\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"ts_from\":1234567890", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryMemoryPointsAsync_SendsHybridRrfQueryWithPayloadFilter()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "result": {
                    "points": [
                      {
                        "id": "11111111-1111-1111-1111-111111111111",
                        "score": 0.91,
                        "payload": {
                          "chunk_id": "11111111-1111-1111-1111-111111111111",
                          "chat_id": "!room:matrix.localhost",
                          "kind": "dialog_chunk"
                        }
                      }
                    ]
                  }
                }
                """)
        });

        var service = CreateService(handler);

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

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/collections/memory_bgem3_v1/points/query", request.Path);
        Assert.Contains("\"fusion\":\"rrf\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"using\":\"text-dense\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"using\":\"text-sparse\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"user_id\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"chat_id\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"peer_id\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"kind\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"with_payload\":true", request.Body, StringComparison.Ordinal);

        var point = Assert.Single(result);
        Assert.Equal("11111111-1111-1111-1111-111111111111", point.PointId);
        Assert.Equal(0.91, point.Score, 3);
        Assert.Equal("!room:matrix.localhost", point.Payload["chat_id"]);
    }

    private static QdrantClient CreateService(RecordingHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:6333")
        };

        var options = Options.Create(new QdrantOptions
        {
            BaseUrl = "http://localhost:6333",
            AutoInitialize = true,
            MemoryCollectionName = "memory_bgem3_v1",
            DenseVectorName = "text-dense",
            SparseVectorName = "text-sparse",
            DenseVectorSize = 1024
        });

        return new QdrantClient(httpClient, options, NullLogger<QdrantClient>.Instance);
    }

    private static void AssertPayloadIndexRequest(RecordedRequest request, string fieldName, string fieldSchema)
    {
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/collections/memory_bgem3_v1/index", request.Path);
        Assert.Contains($"\"field_name\":\"{fieldName}\"", request.Body, StringComparison.Ordinal);
        Assert.Contains($"\"field_schema\":\"{fieldSchema}\"", request.Body, StringComparison.Ordinal);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.AbsolutePath,
                request.RequestUri.Query,
                body));

            return responseFactory(request);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path, string Query, string Body);
}
