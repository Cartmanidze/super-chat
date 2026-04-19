using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Infrastructure.Features.Integrations.Telegram.Userbot;

namespace SuperChat.Tests;

public sealed class TelegramUserbotClientTests
{
    [Fact]
    public async Task StartConnectAsync_SendsPhone_AndParsesPhoneCodeHash()
    {
        var handler = new CapturingHttpHandler((request, _) =>
        {
            var payload = new { status = "awaiting_code", phone_code_hash = "abc123" };
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = JsonContent.Create(payload)
            };
        });
        var client = new TelegramUserbotClient(CreateHttpClient(handler), NullLogger<TelegramUserbotClient>.Instance);

        var result = await client.StartConnectAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "+70001112233",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("abc123", result.PhoneCodeHash);
        Assert.Single(handler.Requests);

        var sent = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, sent.Request.Method);
        Assert.EndsWith("/sessions/11111111111111111111111111111111/connect", sent.Request.RequestUri!.AbsolutePath);

        var body = JsonSerializer.Deserialize<JsonElement>(sent.BodyContent);
        Assert.Equal("+70001112233", body.GetProperty("phone").GetString());
    }

    [Fact]
    public async Task SubmitCodeAsync_Returns202Accepted_AsAwaitingPassword()
    {
        var handler = new CapturingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new TelegramUserbotClient(CreateHttpClient(handler), NullLogger<TelegramUserbotClient>.Instance);

        var result = await client.SubmitCodeAsync(
            Guid.NewGuid(),
            "12345",
            CancellationToken.None);

        Assert.Equal(TelegramUserbotConnectStatus.AwaitingPassword, result.Status);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsNotStarted_OnHttp404()
    {
        var handler = new CapturingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new TelegramUserbotClient(CreateHttpClient(handler), NullLogger<TelegramUserbotClient>.Instance);

        var status = await client.GetStatusAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(TelegramUserbotConnectStatus.NotStarted, status);
    }

    [Fact]
    public void ParseConnectStatus_NormalisesKnownValues()
    {
        Assert.Equal(TelegramUserbotConnectStatus.Connected, TelegramUserbotClient.ParseConnectStatus("connected"));
        Assert.Equal(TelegramUserbotConnectStatus.AwaitingPassword, TelegramUserbotClient.ParseConnectStatus("awaiting_password"));
        Assert.Equal(TelegramUserbotConnectStatus.Failed, TelegramUserbotClient.ParseConnectStatus("failed"));
        Assert.Equal(TelegramUserbotConnectStatus.Unknown, TelegramUserbotClient.ParseConnectStatus("strange"));
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://telegram-userbot-service:7491/")
        };
    }

    private sealed class CapturingHttpHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request, body));
            return responder(request, cancellationToken);
        }
    }

    private sealed record RecordedRequest(HttpRequestMessage Request, string BodyContent);
}
