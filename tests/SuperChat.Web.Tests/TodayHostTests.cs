using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Domain.Features.Intelligence;
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
    public async Task TodayPage_LoadsForAuthenticatedUser_WhenCardsExist()
    {
        var userId = Guid.NewGuid();
        const string email = "today@example.com";
        const string token = "today-test-token";

        await SeedTodayDataAsync(userId, email, token);

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var verifyResponse = await client.GetAsync($"/auth/verify?token={token}");
        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);

        var response = await client.GetAsync("/today");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("?handler=Complete", content, StringComparison.Ordinal);
        Assert.Contains("?handler=Dismiss", content, StringComparison.Ordinal);
        Assert.Contains("class=\"signal-feedback\"", content, StringComparison.Ordinal);
        Assert.Contains("class=\"signal-feedback-link\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("action-link action-link-muted", content, StringComparison.Ordinal);
        Assert.Contains("Недавно закрыто автоматически", content, StringComparison.Ordinal);
    }

    private async Task SeedTodayDataAsync(Guid userId, string email, string token)
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

        dbContext.TelegramConnections.Add(new TelegramConnectionEntity
        {
            UserId = userId,
            State = TelegramConnectionState.Connected,
            UpdatedAt = now
        });

        dbContext.WorkItems.Add(new WorkItemEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = ExtractedItemKind.WaitingOn,
            Title = "Ответить Марине",
            Summary = "Марина ждет подтверждение по смете.",
            SourceRoom = "Team chat",
            SourceEventId = "$waiting-1",
            ObservedAt = now.AddMinutes(-30),
            DueAt = now.AddHours(1),
            Confidence = 0.95,
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.WorkItems.Add(new WorkItemEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = ExtractedItemKind.Commitment,
            Title = "Отправить дек",
            Summary = "Финальный дек уже отправлен.",
            SourceRoom = "Sales",
            SourceEventId = "$resolved-1",
            ObservedAt = now.AddHours(-2),
            DueAt = now.AddHours(-1),
            Confidence = 0.91,
            ResolvedAt = now.AddMinutes(-20),
            ResolutionKind = "completed",
            ResolutionSource = "auto_ai_completion",
            ResolutionConfidence = 0.93,
            ResolutionModel = "deepseek-reasoner",
            ResolutionEvidenceJson = "[\"$evt-done\"]",
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddMinutes(-20)
        });

        dbContext.Meetings.Add(new MeetingEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Созвон с партнером",
            Summary = "Подтвержденный созвон сегодня вечером.",
            SourceRoom = "Partners",
            SourceEventId = "$meeting-1",
            ObservedAt = now.AddMinutes(-10),
            ScheduledFor = now.AddHours(2),
            Confidence = 0.92,
            CreatedAt = now,
            UpdatedAt = now
        });

        await dbContext.SaveChangesAsync();
    }
}
