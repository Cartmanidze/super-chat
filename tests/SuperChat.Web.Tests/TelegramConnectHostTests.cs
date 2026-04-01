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
public sealed class TelegramConnectHostTests : IClassFixture<WebTestApplicationFactory>
{
    private readonly WebTestApplicationFactory _factory;

    public TelegramConnectHostTests(WebTestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConnectTelegramPage_LoadsForAuthenticatedUser_WithoutExistingConnection()
    {
        const string email = "telegram-connect@example.com";
        await SeedUserAsync(email);
        using var client = await CreateAuthenticatedClientAsync(email);

        var response = await client.GetAsync("/connect/telegram");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"premium-card connection-panel\"", content, StringComparison.Ordinal);
        Assert.Contains("?handler=StartChatLogin", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartChatLogin_ShowsExplicitSuccessFeedback()
    {
        const string email = "telegram-connect-success@example.com";
        await SeedUserAsync(email);
        using var client = await CreateAuthenticatedClientAsync(email);

        var connectPage = await client.GetAsync("/connect/telegram");
        var connectContent = await connectPage.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgeryToken(connectContent);
        var connectCookies = connectPage.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

        var request = new HttpRequestMessage(HttpMethod.Post, "/connect/telegram?handler=StartChatLogin");
        AddCookies(request, connectCookies);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken
        });

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var followUp = await client.GetAsync(response.Headers.Location?.ToString() ?? "/connect/telegram");
        var followUpContent = WebUtility.HtmlDecode(await followUp.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, followUp.StatusCode);
        Assert.Contains("Аккаунт подтверждён. SuperChat начнёт подтягивать новые рабочие сигналы из Telegram.", followUpContent, StringComparison.Ordinal);
        Assert.Contains("Telegram подключён", followUpContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitLoginInput_ShowsValidationError_ForEmptyCode()
    {
        const string email = "telegram-connect-validation@example.com";
        var userId = await SeedUserAsync(email);
        await SeedTelegramConnectionAsync(userId, TelegramConnectionState.LoginAwaitingCode);
        using var client = await CreateAuthenticatedClientAsync(email);

        var connectPage = await client.GetAsync("/connect/telegram");
        var connectContent = await connectPage.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgeryToken(connectContent);
        var connectCookies = connectPage.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

        var request = new HttpRequestMessage(HttpMethod.Post, "/connect/telegram?handler=SubmitLoginInput");
        AddCookies(request, connectCookies);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["LoginInput"] = string.Empty,
            ["__RequestVerificationToken"] = antiforgeryToken
        });

        var response = await client.SendAsync(request);
        var responseContent = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Введите код подтверждения из Telegram.", responseContent, StringComparison.Ordinal);
        Assert.Contains("field-validation-error", responseContent, StringComparison.Ordinal);
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
        var verifyCookies = verifyPage.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

        var verifyRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/verify");
        AddCookies(verifyRequest, verifyCookies);
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

    private async Task<Guid> SeedUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SuperChatDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();

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

        await dbContext.SaveChangesAsync();
        return userId;
    }

    private async Task SeedTelegramConnectionAsync(Guid userId, TelegramConnectionState state)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SuperChatDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        dbContext.TelegramConnections.Add(new TelegramConnectionEntity
        {
            UserId = userId,
            State = state,
            ManagementRoomId = "!management:matrix.test",
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static void AddCookies(HttpRequestMessage request, IEnumerable<string> cookies)
    {
        var cookieValues = cookies.ToArray();
        if (cookieValues.Length == 0)
        {
            return;
        }

        request.Headers.Add("Cookie", cookieValues);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        if (!match.Success)
        {
            match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
        }

        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
