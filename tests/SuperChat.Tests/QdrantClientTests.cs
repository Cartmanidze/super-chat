using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
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
                body));

            return responseFactory(request);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path, string Body);
}
