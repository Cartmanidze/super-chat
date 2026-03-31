using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
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

        var response = await client.GetAsync("/connect/telegram");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"premium-card connection-panel\"", content, StringComparison.Ordinal);
        Assert.Contains("?handler=StartChatLogin", content, StringComparison.Ordinal);
    }

    private async Task SeedUserAsync(string email)
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
