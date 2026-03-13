using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class DeepSeekJsonClientTests
{
    [Fact]
    public async Task CompleteJsonAsync_SendsJsonModeRequestAndParsesResponse()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"assistant_text\":\"Short answer\",\"items\":[]}"
                      }
                    }
                  ]
                }
                """)
        });

        var client = CreateClient(handler);

        var response = await client.CompleteJsonAsync<TestJsonResponse>(
            [
                new DeepSeekMessage("system", "Return json."),
                new DeepSeekMessage("user", "Question in json.")
            ],
            320,
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("Short answer", response!.AssistantText);
        Assert.Equal("/chat/completions", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("\"response_format\":{\"type\":\"json_object\"}", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("\"max_tokens\":320", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("\"temperature\":0.1", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractJsonObject_StripsMarkdownFenceWrapper()
    {
        var content =
            """
            ```json
            {"assistant_text":"ok","items":[]}
            ```
            """;

        var extracted = DeepSeekJsonClient.ExtractJsonObject(content);

        Assert.Equal("{\"assistant_text\":\"ok\",\"items\":[]}", extracted);
    }

    private static DeepSeekJsonClient CreateClient(RecordingHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com")
        };

        var options = Options.Create(new DeepSeekOptions
        {
            BaseUrl = "https://api.deepseek.com",
            ApiKey = "test-key",
            Model = "deepseek-chat"
        });

        return new DeepSeekJsonClient(httpClient, options, NullLogger<DeepSeekJsonClient>.Instance);
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

    private sealed record TestJsonResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("assistant_text")] string AssistantText);
}
