using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Messaging;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class MessageNormalizationServiceTests
{
    [Fact]
    public async Task TryStoreAsync_DispatchesPipelineCommands_WhenMessageIsNew()
    {
        var scheduler = new RecordingPipelineCommandScheduler();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var service = new MessageNormalizationService(factory, scheduler, NullLogger<MessageNormalizationService>.Instance);
        var sentAt = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero);

        var stored = await service.TryStoreAsync(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "telegram",
            "!room:matrix.localhost",
            "$evt-1",
            "Alice",
            "Please send the proposal.",
            sentAt,
            CancellationToken.None);

        Assert.True(stored);
        Assert.Single(scheduler.Dispatches);

        var dispatch = scheduler.Dispatches[0];
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), dispatch.UserId);
        Assert.Equal("telegram", dispatch.Source);
        Assert.Equal("!room:matrix.localhost", dispatch.ExternalChatId);
        Assert.Equal(sentAt, dispatch.SentAt);
    }

    [Fact]
    public async Task TryStoreAsync_DoesNotDispatchPipelineCommands_ForDuplicateMessage()
    {
        var scheduler = new RecordingPipelineCommandScheduler();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var service = new MessageNormalizationService(factory, scheduler, NullLogger<MessageNormalizationService>.Instance);
        var sentAt = new DateTimeOffset(2026, 03, 13, 10, 00, 00, TimeSpan.Zero);
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        await service.TryStoreAsync(
            userId,
            "telegram",
            "!room:matrix.localhost",
            "$evt-1",
            "Alice",
            "First copy",
            sentAt,
            CancellationToken.None);

        var stored = await service.TryStoreAsync(
            userId,
            "telegram",
            "!room:matrix.localhost",
            "$evt-1",
            "Alice",
            "Duplicate copy",
            sentAt,
            CancellationToken.None);

        Assert.False(stored);
        Assert.Single(scheduler.Dispatches);
    }

    [Fact]
    public async Task SearchRecentMessagesAsync_FiltersByQueryCaseInsensitivelyOrdersByRecencyAndAppliesLimit()
    {
        var scheduler = new RecordingPipelineCommandScheduler();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var service = new MessageNormalizationService(factory, scheduler, NullLogger<MessageNormalizationService>.Instance);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 04, 11, 10, 00, 00, TimeSpan.Zero);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.NormalizedMessages.AddRange(
            [
                new NormalizedMessageEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Source = "telegram",
                    ExternalChatId = "!sales:matrix.localhost",
                    ExternalMessageId = "$msg-text-newest",
                    SenderName = "Anna",
                    Text = "Please review the CONTRACT today",
                    SentAt = baseTime.AddMinutes(5),
                    ReceivedAt = baseTime,
                    Processed = false
                },
                new NormalizedMessageEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Source = "telegram",
                    ExternalChatId = "!sales:matrix.localhost",
                    ExternalMessageId = "$msg-text-older",
                    SenderName = "Boris",
                    Text = "old contract reminder",
                    SentAt = baseTime.AddMinutes(2),
                    ReceivedAt = baseTime,
                    Processed = false
                },
                new NormalizedMessageEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Source = "telegram",
                    ExternalChatId = "!sales:matrix.localhost",
                    ExternalMessageId = "$msg-text-oldest",
                    SenderName = "Carl",
                    Text = "talking about the Contract draft",
                    SentAt = baseTime.AddMinutes(1),
                    ReceivedAt = baseTime,
                    Processed = false
                },
                new NormalizedMessageEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Source = "telegram",
                    ExternalChatId = "!cafeteria:matrix.localhost",
                    ExternalMessageId = "$msg-unrelated",
                    SenderName = "Dora",
                    Text = "lunch is ready",
                    SentAt = baseTime.AddMinutes(10),
                    ReceivedAt = baseTime,
                    Processed = false
                },
                new NormalizedMessageEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = otherUserId,
                    Source = "telegram",
                    ExternalChatId = "!sales:matrix.localhost",
                    ExternalMessageId = "$msg-other-user",
                    SenderName = "Eve",
                    Text = "contract for someone else",
                    SentAt = baseTime.AddMinutes(20),
                    ReceivedAt = baseTime,
                    Processed = false
                }
            ]);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var results = await service.SearchRecentMessagesAsync(userId, "contract", limit: 2, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal("$msg-text-newest", results[0].ExternalMessageId);
        Assert.Equal("$msg-text-older", results[1].ExternalMessageId);
        Assert.DoesNotContain(results, message => message.ExternalMessageId == "$msg-unrelated");
        Assert.DoesNotContain(results, message => message.ExternalMessageId == "$msg-other-user");
    }

    [Fact]
    public async Task SearchRecentMessagesAsync_ReturnsEmpty_WhenQueryIsBlank()
    {
        var scheduler = new RecordingPipelineCommandScheduler();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var service = new MessageNormalizationService(factory, scheduler, NullLogger<MessageNormalizationService>.Instance);

        var results = await service.SearchRecentMessagesAsync(Guid.NewGuid(), "   ", limit: 20, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchRecentMessagesAsync_EscapesLikeWildcards_SoLiteralPercentMatchesOnly()
    {
        var scheduler = new RecordingPipelineCommandScheduler();
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var service = new MessageNormalizationService(factory, scheduler, NullLogger<MessageNormalizationService>.Instance);
        var userId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 04, 11, 10, 00, 00, TimeSpan.Zero);

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.NormalizedMessages.AddRange(
            [
                new NormalizedMessageEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Source = "telegram",
                    ExternalChatId = "!sales:matrix.localhost",
                    ExternalMessageId = "$msg-literal-percent",
                    SenderName = "Anna",
                    Text = "Discount is 50% today",
                    SentAt = baseTime.AddMinutes(5),
                    ReceivedAt = baseTime,
                    Processed = false
                },
                new NormalizedMessageEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Source = "telegram",
                    ExternalChatId = "!sales:matrix.localhost",
                    ExternalMessageId = "$msg-no-percent",
                    SenderName = "Boris",
                    Text = "no discount today",
                    SentAt = baseTime.AddMinutes(4),
                    ReceivedAt = baseTime,
                    Processed = false
                }
            ]);

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var results = await service.SearchRecentMessagesAsync(userId, "50%", limit: 10, CancellationToken.None);

        var match = Assert.Single(results);
        Assert.Equal("$msg-literal-percent", match.ExternalMessageId);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-normalization-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return factory;
    }

    private sealed class RecordingPipelineCommandScheduler : IPipelineCommandScheduler
    {
        public List<RecordedDispatch> Dispatches { get; } = [];

        public bool RequiresTransactionalDispatch => false;

        public Task DispatchNormalizedMessageStoredAsync(
            SuperChatDbContext dbContext,
            Guid userId,
            string source,
            string externalChatId,
            Guid normalizedMessageId,
            string externalMessageId,
            DateTimeOffset sentAt,
            CancellationToken cancellationToken)
        {
            Dispatches.Add(new RecordedDispatch(userId, source, externalChatId, normalizedMessageId, externalMessageId, sentAt));
            return Task.CompletedTask;
        }
    }

    private sealed record RecordedDispatch(
        Guid UserId,
        string Source,
        string ExternalChatId,
        Guid NormalizedMessageId,
        string ExternalMessageId,
        DateTimeOffset SentAt);

    private sealed class TestDbContextFactory(DbContextOptions<SuperChatDbContext> options) : IDbContextFactory<SuperChatDbContext>
    {
        public SuperChatDbContext CreateDbContext()
        {
            return new SuperChatDbContext(options);
        }

        public Task<SuperChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SuperChatDbContext(options));
        }
    }
}
