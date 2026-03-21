using System.Net;
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

        var verifyResponse = await client.GetAsync($"/auth/verify?token={token}");
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

        dbContext.MagicLinks.Add(new MagicLinkTokenEntity
        {
            Value = token,
            Email = email,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(15),
            Consumed = false
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
}
