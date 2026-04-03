using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Web.Tests;

[Collection("web-host")]
public sealed class TodayHostTests : IClassFixture<WebTestApplicationFactory>
{
    private readonly WebTestApplicationFactory _factory;

    public TodayHostTests(WebTestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TodayPage_LoadsForAuthenticatedUser_WhenMeetingsExist()
    {
        var userId = Guid.NewGuid();
        const string email = "today@example.com";

        await SeedTodayDataAsync(userId, email);

        using var client = await CreateAuthenticatedClientAsync(email);

        var response = await client.GetAsync("/today");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"page-hero insight-hero\"", content, StringComparison.Ordinal);
        Assert.Contains("?handler=Complete", content, StringComparison.Ordinal);
        Assert.Contains("?handler=Dismiss", content, StringComparison.Ordinal);
        Assert.Contains("class=\"signal-feedback\"", content, StringComparison.Ordinal);
        Assert.Contains("class=\"signal-card signal-card-focus\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TodayPage_ShowsEmptyState_WhenNoMeetingsExist()
    {
        var userId = Guid.NewGuid();
        const string email = "today-empty@example.com";

        await SeedTodayDataAsync(userId, email, includeMeeting: false);

        using var client = await CreateAuthenticatedClientAsync(email);

        var response = await client.GetAsync("/today");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"lane-empty\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("?handler=Complete", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TodayPage_ShowsConnectPrompt_WhenTelegramIsNotConnected()
    {
        var userId = Guid.NewGuid();
        const string email = "today-disconnected@example.com";

        await SeedTodayDataAsync(userId, email, telegramConnected: false, includeMeeting: false);

        using var client = await CreateAuthenticatedClientAsync(email);

        var response = await client.GetAsync("/today");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"status-badge status-warning\"", content, StringComparison.Ordinal);
        Assert.Contains("class=\"inline-actions\"", content, StringComparison.Ordinal);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var verifyPage = await client.GetAsync($"/auth/verify?email={Uri.EscapeDataString(email)}");
        var verifyContent = await verifyPage.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgeryToken(verifyContent);
        var verifyCookies = verifyPage.Headers.GetValues("Set-Cookie");

        var verifyRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/verify");
        verifyRequest.Headers.Add("Cookie", verifyCookies);
        verifyRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Code"] = "123456",
            ["__RequestVerificationToken"] = antiforgeryToken
        });

        var verifyResponse = await client.SendAsync(verifyRequest);
        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);

        return client;
    }

    private async Task SeedTodayDataAsync(
        Guid userId,
        string email,
        bool telegramConnected = true,
        bool includeMeeting = true)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SuperChatDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var now = DateTimeOffset.UtcNow;

        dbContext.AppUsers.Add(new AppUserEntity
        {
            Id = userId,
            Email = email,
            CreatedAt = now,
            LastSeenAt = now
        });

        var salt = RandomNumberGenerator.GetBytes(16);
        var codeBytes = "123456"u8.ToArray();
        var hashInput = new byte[salt.Length + codeBytes.Length];
        salt.CopyTo(hashInput, 0);
        codeBytes.CopyTo(hashInput, salt.Length);

        dbContext.VerificationCodes.Add(new VerificationCodeEntity
        {
            Id = Guid.NewGuid(),
            Email = email,
            CodeHash = Convert.ToBase64String(SHA256.HashData(hashInput)),
            CodeSalt = Convert.ToBase64String(salt),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10),
            Consumed = false,
            FailedAttempts = 0
        });

        dbContext.TelegramConnections.Add(new TelegramConnectionEntity
        {
            UserId = userId,
            State = telegramConnected ? TelegramConnectionState.Connected : TelegramConnectionState.NotStarted,
            UpdatedAt = now
        });

        if (includeMeeting)
        {
            dbContext.Meetings.Add(new MeetingEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "Созвон с партнёром",
                Summary = "Подтверждённый созвон сегодня вечером.",
                SourceRoom = "Partners",
                SourceEventId = "$meeting-1",
                ObservedAt = now.AddMinutes(-10),
                ScheduledFor = now.AddHours(2),
                Confidence = 0.92,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\" />");
        Assert.True(match.Success);
        return match.Groups[1].Value;
    }
}
