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
                  "title": "Psychotron Forum",
                  "is_broadcast_channel": false
                }
                """)
        });

        var service = CreateService(handler);

        var result = await service.GetRoomInfoAsync("@pilot:matrix.localhost", "!group:matrix.localhost", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("channel", result!.PeerType);
        Assert.Equal(356, result.ParticipantCount);
        Assert.Equal("Psychotron Forum", result.Title);
        Assert.False(result.IsBroadcastChannel);
        Assert.Equal("/rooms/%21group%3Amatrix.localhost/info", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("matrixUserId=%40pilot%3Amatrix.localhost", handler.LastRequest.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRoomInfoAsync_ParsesBroadcastChannelFlag()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "room_id": "!channel:matrix.localhost",
                  "peer_type": "channel",
                  "participant_count": 127000,
                  "title": "Announcements",
                  "is_broadcast_channel": true
                }
                """)
        });

        var service = CreateService(handler);

        var result = await service.GetRoomInfoAsync("@pilot:matrix.localhost", "!channel:matrix.localhost", CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsBroadcastChannel);
    }

    [Fact]
    public async Task GetRoomInfoAsync_ReturnsNullForNotFound()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = CreateService(handler);

        var result = await service.GetRoomInfoAsync("@pilot:matrix.localhost", "!missing:matrix.localhost", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSenderInfoAsync_ReturnsParsedSenderInfo()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "sender_mxid": "@telegram_12345:matrix.localhost",
                  "telegram_user_id": 12345,
                  "is_bot": true
                }
                """)
        });

        var service = CreateService(handler);

        var result = await service.GetSenderInfoAsync(
            "@pilot:matrix.localhost",
            "@telegram_12345:matrix.localhost",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("@telegram_12345:matrix.localhost", result!.SenderMatrixUserId);
        Assert.Equal(12345, result.TelegramUserId);
        Assert.True(result.IsBot);
        Assert.Equal("/senders/%40telegram_12345%3Amatrix.localhost/info", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("matrixUserId=%40pilot%3Amatrix.localhost", handler.LastRequest.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSenderInfoAsync_ReturnsNullForNotFound()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = CreateService(handler);

        var result = await service.GetSenderInfoAsync(
            "@pilot:matrix.localhost",
            "@telegram_99999:matrix.localhost",
            CancellationToken.None);

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
