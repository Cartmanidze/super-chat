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
