using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Features.Intelligence.Resolution;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Features.Operations;
using SuperChat.Infrastructure.Shared.Persistence;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Tests;

public sealed class ConversationResolutionCommandHandlerTests
{
    [Fact]
    public async Task Handle_ResolvesConversationWorkItemsWithoutReadPath()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();
        var roomId = "!sales:matrix.localhost";

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.Commitment,
                Title = "You promised to send the deck",
                Summary = "Need to send the final deck.",
                SourceRoom = roomId,
                SourceEventId = "$evt-commitment",
                ObservedAt = observedAt,
                Confidence = 0.88,
                CreatedAt = observedAt,
                UpdatedAt = observedAt
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = "$evt-done",
                SenderName = "You",
                Text = "готово, отправил финальный дек",
                SentAt = observedAt.AddMinutes(7),
                IngestedAt = observedAt.AddMinutes(7),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var handler = new ResolveConversationItemsCommandHandler(
            new ConversationResolutionService(
                factory,
                new NoOpAiResolutionService(),
                new WorkItemAutoResolutionService(factory, NullLogger<WorkItemAutoResolutionService>.Instance),
                new MeetingAutoResolutionService(factory, NullLogger<MeetingAutoResolutionService>.Instance),
                Options.Create(new ResolutionOptions
                {
                    UseLlm = false
                }),
                NullLogger<ConversationResolutionService>.Instance),
            new FixedTimeProvider(observedAt.AddMinutes(10)),
            new TestHostApplicationLifetime(),
            NullLogger<ResolveConversationItemsCommandHandler>.Instance);

        await handler.Handle(new ResolveConversationItemsCommand(userId, roomId));

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.WorkItems.SingleAsync(item => item.Id == itemId, CancellationToken.None);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal(WorkItemResolutionState.Completed, entity.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.AutoCompletion, entity.ResolutionSource);
    }

    [Fact]
    public async Task Handle_PersistsAiResolutionTrace()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();
        var roomId = "!sales:matrix.localhost";

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.Commitment,
                Title = "Send the deck",
                Summary = "Need to send the final deck.",
                SourceRoom = roomId,
                SourceEventId = "$evt-commitment",
                ObservedAt = observedAt,
                Confidence = 0.88,
                CreatedAt = observedAt,
                UpdatedAt = observedAt
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = "$evt-done",
                SenderName = "You",
                Text = "готово, отправил финальный дек",
                SentAt = observedAt.AddMinutes(7),
                IngestedAt = observedAt.AddMinutes(7),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var handler = new ResolveConversationItemsCommandHandler(
            new ConversationResolutionService(
                factory,
                new StubAiResolutionService(
                [
                    new AiResolutionDecisionResult(
                        itemId,
                        WorkItemResolutionState.Completed,
                        WorkItemResolutionState.AutoAiCompletion,
                        observedAt.AddMinutes(7),
                        0.93,
                        "deepseek-reasoner",
                        ["$evt-done"])
                ]),
                new WorkItemAutoResolutionService(factory, NullLogger<WorkItemAutoResolutionService>.Instance),
                new MeetingAutoResolutionService(factory, NullLogger<MeetingAutoResolutionService>.Instance),
                Options.Create(new ResolutionOptions
                {
                    UseLlm = true
                }),
                NullLogger<ConversationResolutionService>.Instance),
            new FixedTimeProvider(observedAt.AddMinutes(10)),
            new TestHostApplicationLifetime(),
            NullLogger<ResolveConversationItemsCommandHandler>.Instance);

        await handler.Handle(new ResolveConversationItemsCommand(userId, roomId));

        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.WorkItems.SingleAsync(item => item.Id == itemId, CancellationToken.None);
        Assert.Equal(WorkItemResolutionState.AutoAiCompletion, entity.ResolutionSource);
        Assert.Equal(0.93, entity.ResolutionConfidence);
        Assert.Equal("deepseek-reasoner", entity.ResolutionModel);
        Assert.Equal("[\"$evt-done\"]", entity.ResolutionEvidenceJson);
    }

    [Fact]
    public async Task Handle_DoesNotThrowForWaitingOnMessageWithoutEventId()
    {
        var factory = await CreateFactoryAsync(CancellationToken.None);
        var userId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero);
        var itemId = Guid.NewGuid();
        var roomId = "!sales:matrix.localhost";

        await using (var dbContext = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            dbContext.WorkItems.Add(new WorkItemEntity
            {
                Id = itemId,
                UserId = userId,
                Kind = ExtractedItemKind.WaitingOn,
                Title = "Жду ответ по смете",
                Summary = "Нужно дождаться финального ответа.",
                SourceRoom = roomId,
                SourceEventId = "$evt-waiting-on",
                ObservedAt = observedAt,
                Confidence = 0.88,
                CreatedAt = observedAt,
                UpdatedAt = observedAt
            });

            dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Source = "telegram",
                MatrixRoomId = roomId,
                MatrixEventId = string.Empty,
                SenderName = "You",
                Text = "готово, уже ответил и отправил детали",
                SentAt = observedAt.AddMinutes(7),
                IngestedAt = observedAt.AddMinutes(7),
                Processed = true
            });

            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var handler = new ResolveConversationItemsCommandHandler(
            new ConversationResolutionService(
                factory,
                new NoOpAiResolutionService(),
                new WorkItemAutoResolutionService(factory, NullLogger<WorkItemAutoResolutionService>.Instance),
                new MeetingAutoResolutionService(factory, NullLogger<MeetingAutoResolutionService>.Instance),
                Options.Create(new ResolutionOptions
                {
                    UseLlm = false
                }),
                NullLogger<ConversationResolutionService>.Instance),
            new FixedTimeProvider(observedAt.AddMinutes(10)),
            new TestHostApplicationLifetime(),
            NullLogger<ResolveConversationItemsCommandHandler>.Instance);

        var exception = await Record.ExceptionAsync(() => handler.Handle(new ResolveConversationItemsCommand(userId, roomId)));

        Assert.Null(exception);
        await using var verificationContext = await factory.CreateDbContextAsync(CancellationToken.None);
        var entity = await verificationContext.WorkItems.SingleAsync(item => item.Id == itemId, CancellationToken.None);
        Assert.NotNull(entity.ResolvedAt);
        Assert.Equal(WorkItemResolutionState.Completed, entity.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.AutoReply, entity.ResolutionSource);
    }

    private static async Task<IDbContextFactory<SuperChatDbContext>> CreateFactoryAsync(CancellationToken cancellationToken)
    {
        var dbContextOptions = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-conversation-resolution-{Guid.NewGuid():N}")
            .Options;

        var factory = new TestDbContextFactory(dbContextOptions);
        await using var dbContext = await factory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return factory;
    }

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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }

    private sealed class NoOpAiResolutionService : IAiResolutionService
    {
        public Task<IReadOnlyList<AiResolutionDecisionResult>> ResolveAsync(
            IReadOnlyList<ConversationResolutionCandidate> candidates,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AiResolutionDecisionResult>>(Array.Empty<AiResolutionDecisionResult>());
        }
    }

    private sealed class StubAiResolutionService(IReadOnlyList<AiResolutionDecisionResult> decisions) : IAiResolutionService
    {
        public Task<IReadOnlyList<AiResolutionDecisionResult>> ResolveAsync(
            IReadOnlyList<ConversationResolutionCandidate> candidates,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(decisions);
        }
    }
}
