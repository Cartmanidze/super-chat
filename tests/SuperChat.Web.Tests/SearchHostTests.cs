using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Web.Tests;

[Collection("web-host")]
public sealed class SearchHostTests : IClassFixture<WebTestApplicationFactory>
{
    private readonly WebTestApplicationFactory _factory;

    public SearchHostTests(WebTestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SearchPage_ShowsAiResolutionNoteAndFeedbackLink()
    {
        var userId = Guid.NewGuid();
        const string email = "search@example.com";
        const string token = "search-test-token";

        await SeedSearchDataAsync(userId, email, token);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Authenticate via the OTP verify form
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

        var response = await client.GetAsync("/search?query=deck");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("search-resolution-note", content, StringComparison.Ordinal);
        Assert.Contains("search-context-resolution", content, StringComparison.Ordinal);
        Assert.Contains("search-context-feedback", content, StringComparison.Ordinal);
        Assert.Contains("/feedback?area=search", content, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedSearchDataAsync(Guid userId, string email, string token)
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

        dbContext.WorkItems.Add(new WorkItemEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = ExtractedItemKind.Commitment,
            Title = "Send deck",
            Summary = "Need to send the final deck.",
            SourceRoom = "Sales",
            SourceEventId = "$evt-search-1",
            ObservedAt = now.AddHours(-2),
            Confidence = 0.89,
            ResolvedAt = now.AddMinutes(-10),
            ResolutionKind = "completed",
            ResolutionSource = "auto_ai_completion",
            ResolutionConfidence = 0.93,
            ResolutionModel = "deepseek-reasoner",
            ResolutionEvidenceJson = "[\"$evt-done\"]",
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddMinutes(-10)
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
