using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class EmbeddingServiceClientTests
{
    [Fact]
    public async Task EmbedAsync_ReturnsParsedEmbeddingPayload()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "dense_vector": [0.1, 0.2, 0.3],
                  "sparse_vector": {
                    "indices": [7, 11],
                    "values": [0.6, 0.4]
                  },
                  "provider": "mock",
                  "model": "BAAI/bge-m3",
                  "embedding_version": "bge-m3-v1"
                }
                """)
        });

        var service = CreateService(handler, denseVectorSize: 3);

        var result = await service.EmbedAsync("hello world", CancellationToken.None);

        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, result.DenseVector);
        Assert.Equal(new long[] { 7, 11 }, result.SparseVector.Indices);
        Assert.Equal(new[] { 0.6f, 0.4f }, result.SparseVector.Values);
        Assert.Equal("mock", result.Provider);
        Assert.Equal("BAAI/bge-m3", result.Model);
        Assert.Equal("bge-m3-v1", result.EmbeddingVersion);
        Assert.Equal("/embed", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("\"text\":\"hello world\"", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmbedAsync_ThrowsForMismatchedSparseVectorArrays()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "dense_vector": [0.1, 0.2, 0.3],
                  "sparse_vector": {
                    "indices": [7, 11],
                    "values": [0.6]
                  },
                  "provider": "mock",
                  "model": "BAAI/bge-m3",
                  "embedding_version": "bge-m3-v1"
                }
                """)
        });

        var service = CreateService(handler, denseVectorSize: 3);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EmbedAsync("hello world", CancellationToken.None));

        Assert.Contains("mismatched sparse vector arrays", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static EmbeddingServiceClient CreateService(RecordingHandler handler, int denseVectorSize)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://embedding-service:7291")
        };

        var options = Options.Create(new EmbeddingOptions
        {
            Enabled = true,
            BaseUrl = "http://embedding-service:7291",
            TimeoutSeconds = 60,
            DenseVectorSize = denseVectorSize
        });

        return new EmbeddingServiceClient(httpClient, options, NullLogger<EmbeddingServiceClient>.Instance);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responseFactory(request);
        }
    }
}
