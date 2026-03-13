using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class MatrixApiClientTests
{
    [Fact]
    public async Task SyncAsync_ReturnsTimelineEventsAndInvitedRooms()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "next_batch": "s123",
                  "account_data": {
                    "events": [
                      {
                        "type": "m.direct",
                        "content": {
                          "@telegram_alice:matrix.localhost": [
                            "!joined:matrix.localhost"
                          ]
                        }
                      }
                    ]
                  },
                  "rooms": {
                    "join": {
                      "!joined:matrix.localhost": {
                        "summary": {
                          "m.joined_member_count": 2,
                          "m.invited_member_count": 0
                        },
                        "timeline": {
                          "events": [
                            {
                              "event_id": "$event-1",
                              "type": "m.room.message",
                              "sender": "@telegram_alice:matrix.localhost",
                              "origin_server_ts": 1710240000000,
                              "content": {
                                "msgtype": "m.text",
                                "body": "Please send the proposal tomorrow."
                              }
                            },
                            {
                              "event_id": "$ignored-state",
                              "type": "m.room.member",
                              "sender": "@telegram_alice:matrix.localhost",
                              "origin_server_ts": 1710240001000,
                              "content": {}
                            },
                            {
                              "event_id": "$ignored-empty",
                              "type": "m.room.message",
                              "sender": "@telegram_alice:matrix.localhost",
                              "origin_server_ts": 1710240002000,
                              "content": {
                                "msgtype": "m.text"
                              }
                            }
                          ]
                        }
                      }
                    },
                    "invite": {
                      "!portal-a:matrix.localhost": {
                        "invite_state": {
                          "events": []
                        }
                      },
                      "!portal-b:matrix.localhost": {
                        "invite_state": {
                          "events": []
                        }
                      }
                    }
                  }
                }
                """)
        });

        var client = CreateClient(handler);

        var result = await client.SyncAsync("access-token", "since-token", CancellationToken.None);

        Assert.Equal("s123", result.NextBatchToken);
        Assert.Equal(["!portal-a:matrix.localhost", "!portal-b:matrix.localhost"], result.InvitedRoomIds);
        Assert.Single(result.Rooms);
        Assert.Equal("!joined:matrix.localhost", result.Rooms[0].RoomId);
        Assert.Equal(2, result.Rooms[0].MemberCount);
        Assert.True(result.Rooms[0].IsDirect);
        Assert.Single(result.Rooms[0].Events);
        Assert.Equal("$event-1", result.Rooms[0].Events[0].EventId);
        Assert.Equal("Please send the proposal tomorrow.", result.Rooms[0].Events[0].Body);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Contains("/_matrix/client/v3/sync", handler.LastRequest.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("since=since-token", handler.LastRequest.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetJoinedMemberCountAsync_ReturnsJoinedMemberCount()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "joined": {
                    "@alice:matrix.localhost": {},
                    "@bob:matrix.localhost": {},
                    "@carol:matrix.localhost": {}
                  }
                }
                """)
        });

        var client = CreateClient(handler);

        var count = await client.GetJoinedMemberCountAsync("access-token", "!room:matrix.localhost", CancellationToken.None);

        Assert.Equal(3, count);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/_matrix/client/v3/rooms/%21room%3Amatrix.localhost/joined_members", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetRoomDisplayNameAsync_ReturnsRoomNameFromState()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                [
                  {
                    "type": "m.room.topic",
                    "content": {
                      "topic": "Quarterly planning"
                    }
                  },
                  {
                    "type": "m.room.name",
                    "content": {
                      "name": "Sales Team"
                    }
                  }
                ]
                """)
        });

        var client = CreateClient(handler);

        var roomName = await client.GetRoomDisplayNameAsync(
            "access-token",
            "!room:matrix.localhost",
            "@superchat-user:matrix.localhost",
            CancellationToken.None);

        Assert.Equal("Sales Team", roomName);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/_matrix/client/v3/rooms/%21room%3Amatrix.localhost/state", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetRoomDisplayNameAsync_ReturnsOtherMemberDisplayName_WhenRoomHasNoName()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                [
                  {
                    "type": "m.room.member",
                    "state_key": "@superchat-user:matrix.localhost",
                    "content": {
                      "membership": "join",
                      "displayname": "Super Chat Hidden User"
                    }
                  },
                  {
                    "type": "m.room.member",
                    "state_key": "@telegram-alice:matrix.localhost",
                    "content": {
                      "membership": "join",
                      "displayname": "Alice"
                    }
                  },
                  {
                    "type": "m.room.member",
                    "state_key": "@telegrambot:matrix.localhost",
                    "content": {
                      "membership": "join",
                      "displayname": "Telegram Bridge"
                    }
                  }
                ]
                """)
        });

        var client = CreateClient(handler);

        var roomName = await client.GetRoomDisplayNameAsync(
            "access-token",
            "!room:matrix.localhost",
            "@superchat-user:matrix.localhost",
            CancellationToken.None);

        Assert.Equal("Alice", roomName);
    }

    [Fact]
    public async Task JoinRoomAsync_PostsToJoinEndpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.JoinRoomAsync("access-token", "!portal:matrix.localhost", CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/_matrix/client/v3/rooms/%21portal%3Amatrix.localhost/join", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("access-token", handler.LastRequest.Headers.Authorization?.Parameter);
    }

    private static MatrixApiClient CreateClient(RecordingHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://matrix.localhost")
        };

        return new MatrixApiClient(
            httpClient,
            Options.Create(new MatrixOptions()),
            NullLogger<MatrixApiClient>.Instance);
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
