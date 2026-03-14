using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class EmbeddingServiceClientTests
{
    [Fact]
    public async Task EmbedAsync_ReturnsParsedLocalServicePayload()
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

        var service = CreateService(
            handler,
            new EmbeddingOptions
            {
                Enabled = true,
                Backend = "LocalService",
                BaseUrl = "http://embedding-service:7291",
                TimeoutSeconds = 60,
                DenseVectorSize = 3
            });

        var result = await service.EmbedAsync("hello world", EmbeddingPurpose.Document, CancellationToken.None);

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
    public async Task EmbedAsync_ThrowsForMismatchedLocalSparseVectorArrays()
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

        var service = CreateService(
            handler,
            new EmbeddingOptions
            {
                Enabled = true,
                Backend = "LocalService",
                BaseUrl = "http://embedding-service:7291",
                TimeoutSeconds = 60,
                DenseVectorSize = 3
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EmbedAsync("hello world", EmbeddingPurpose.Document, CancellationToken.None));

        Assert.Contains("mismatched sparse vector arrays", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmbedAsync_UsesYandexQueryModelForQueryEmbeddings()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "embedding": [0.11, 0.22, 0.33],
                  "modelVersion": "2026-03-01"
                }
                """)
        });

        var service = CreateService(
            handler,
            new EmbeddingOptions
            {
                Enabled = true,
                Backend = "YandexCloud",
                YandexBaseUrl = "https://ai.api.cloud.yandex.net",
                YandexApiKey = "yc-secret",
                YandexFolderId = "b1g-folder",
                YandexDocModelName = "text-search-doc",
                YandexQueryModelName = "text-search-query",
                DenseVectorSize = 3
            });

        var result = await service.EmbedAsync("Что я обещал Ивану?", EmbeddingPurpose.Query, CancellationToken.None);

        Assert.Equal("yandex_cloud", result.Provider);
        Assert.Equal("emb://b1g-folder/text-search-query/latest", result.Model);
        Assert.Equal("emb://b1g-folder/text-search-query/latest:2026-03-01", result.EmbeddingVersion);
        Assert.Equal("/foundationModels/v1/textEmbedding", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("Api-Key yc-secret", handler.LastRequest.Headers.Authorization!.ToString());

        using var requestDocument = JsonDocument.Parse(handler.LastRequestBody);
        Assert.Equal(
            "emb://b1g-folder/text-search-query/latest",
            requestDocument.RootElement.GetProperty("modelUri").GetString());
        Assert.Equal(
            "Что я обещал Ивану?",
            requestDocument.RootElement.GetProperty("text").GetString());

        Assert.NotEmpty(result.SparseVector.Indices);
        Assert.Equal(result.SparseVector.Indices.Count, result.SparseVector.Values.Count);
    }

    [Fact]
    public async Task EmbedAsync_UsesExplicitYandexDocumentModelUriWhenConfigured()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "embedding": [0.1, 0.2],
                  "modelVersion": "2026-02-15"
                }
                """)
        });

        var service = CreateService(
            handler,
            new EmbeddingOptions
            {
                Enabled = true,
                Backend = "YandexCloud",
                YandexBaseUrl = "https://ai.api.cloud.yandex.net",
                YandexApiKey = "yc-secret",
                YandexDocModelUri = "emb://folder-x/text-search-doc/latest",
                DenseVectorSize = 2
            });

        var result = await service.EmbedAsync("Contract summary", EmbeddingPurpose.Document, CancellationToken.None);

        Assert.Equal("emb://folder-x/text-search-doc/latest", result.Model);
        Assert.Contains("\"modelUri\":\"emb://folder-x/text-search-doc/latest\"", handler.LastRequestBody, StringComparison.Ordinal);
    }

    private static EmbeddingServiceClient CreateService(RecordingHandler handler, EmbeddingOptions embeddingOptions)
    {
        var baseUrl = embeddingOptions.Backend.Equals("YandexCloud", StringComparison.OrdinalIgnoreCase)
            ? embeddingOptions.YandexBaseUrl
            : embeddingOptions.BaseUrl;

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl)
        };

        return new EmbeddingServiceClient(httpClient, Options.Create(embeddingOptions), NullLogger<EmbeddingServiceClient>.Instance);
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
