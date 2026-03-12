using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class TelegramRoomInfoServiceTests
{
    [Fact]
    public async Task GetRoomInfoAsync_ReturnsParsedRoomInfo()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "room_id": "!group:matrix.localhost",
                  "peer_type": "channel",
                  "participant_count": 356,
                  "title": "Психотронная форум🧪"
                }
                """)
        });

        var service = CreateService(handler);

        var result = await service.GetRoomInfoAsync("@pilot:matrix.localhost", "!group:matrix.localhost", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("channel", result!.PeerType);
        Assert.Equal(356, result.ParticipantCount);
        Assert.Equal("Психотронная форум🧪", result.Title);
        Assert.Equal("/rooms/%21group%3Amatrix.localhost/info", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("matrixUserId=%40pilot%3Amatrix.localhost", handler.LastRequest.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRoomInfoAsync_ReturnsNullForNotFound()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = CreateService(handler);

        var result = await service.GetRoomInfoAsync("@pilot:matrix.localhost", "!missing:matrix.localhost", CancellationToken.None);

        Assert.Null(result);
    }

    private static TelegramRoomInfoService CreateService(RecordingHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://mautrix-telegram-helper:29318")
        };

        return new TelegramRoomInfoService(httpClient, NullLogger<TelegramRoomInfoService>.Instance);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responseFactory(request));
        }
    }
}
