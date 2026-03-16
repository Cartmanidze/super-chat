using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class ChatAnswerGenerationServiceTests
{
    [Fact]
    public async Task TryGenerateAsync_ReturnsNull_WhenDisabled()
    {
        var service = CreateService(
            """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"assistant_text\":\"answer\",\"items\":[]}"
                  }
                }
              ]
            }
            """,
            new ChatAnsweringOptions
            {
                Enabled = false
            });

        var result = await service.TryGenerateAsync(
            "What did I promise Ivan?",
            [new ChatAnswerContextItem("ctx_1", "Ivan", DateTimeOffset.UtcNow, "Some text")],
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryGenerateAsync_ReturnsMappedItemsFromKnownReferences()
    {
        var service = CreateService(
            """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"assistant_text\":\"You promised Ivan that you would send the proposal today.\",\"items\":[{\"reference_key\":\"ctx_1\",\"title\":\"Promise to Ivan\",\"summary\":\"You explicitly said you would send the proposal today.\"}]}"
                  }
                }
              ]
            }
            """);

        var result = await service.TryGenerateAsync(
            "What did I promise Ivan?",
            [new ChatAnswerContextItem("ctx_1", "Ivan", DateTimeOffset.UtcNow, "Ivan: Please send the proposal tomorrow.\nYou: I will send it today.")],
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("You promised Ivan that you would send the proposal today.", result!.AssistantText);
        var item = Assert.Single(result.Items);
        Assert.Equal("ctx_1", item.ReferenceKey);
        Assert.Equal("Promise to Ivan", item.Title);
    }

    [Fact]
    public async Task TryGenerateAsync_DropsUnknownReferences()
    {
        var service = CreateService(
            """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"assistant_text\":\"Context is mixed.\",\"items\":[{\"reference_key\":\"ctx_99\",\"title\":\"Unknown\",\"summary\":\"Should be dropped.\"}]}"
                  }
                }
              ]
            }
            """);

        var result = await service.TryGenerateAsync(
            "What happened?",
            [new ChatAnswerContextItem("ctx_1", "Ivan", DateTimeOffset.UtcNow, "Some text")],
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Context is mixed.", result!.AssistantText);
        Assert.Empty(result.Items);
    }

    private static ChatAnswerGenerationService CreateService(
        string responseJson,
        ChatAnsweringOptions? answeringOptions = null)
    {
        var client = new DeepSeekJsonClient(
            new HttpClient(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            }))
            {
                BaseAddress = new Uri("https://api.deepseek.com")
            },
            Options.Create(new DeepSeekOptions
            {
                BaseUrl = "https://api.deepseek.com",
                ApiKey = "test-key",
                Model = "deepseek-reasoner"
            }),
            NullLogger<DeepSeekJsonClient>.Instance);

        return new ChatAnswerGenerationService(
            client,
            Options.Create(answeringOptions ?? new ChatAnsweringOptions()),
            NullLogger<ChatAnswerGenerationService>.Instance);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
