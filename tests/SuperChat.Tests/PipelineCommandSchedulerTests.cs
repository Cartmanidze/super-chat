using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Rebus.Bus;
using Rebus.Bus.Advanced;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Operations;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class PipelineCommandSchedulerTests
{
    [Fact]
    public async Task OneWayScheduler_DispatchesCommandsToConfiguredQueue()
    {
        var routing = Substitute.For<IRoutingApi>();
        var advanced = Substitute.For<IAdvancedApi>();
        advanced.Routing.Returns(routing);

        var bus = Substitute.For<IBus>();
        bus.Advanced.Returns(advanced);

        var scheduler = new OneWayClientPipelineCommandScheduler(
            bus,
            Options.Create(new ChunkingOptions
            {
                MaxGapMinutes = 15
            }),
            Options.Create(new PipelineMessagingOptions
            {
                InputQueueName = "superchat-pipeline"
            }),
            Options.Create(new PersistenceOptions
            {
                Provider = "Sqlite"
            }),
            NullLogger<OneWayClientPipelineCommandScheduler>.Instance);

        using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var sentAt = new DateTimeOffset(2026, 04, 03, 10, 00, 00, TimeSpan.Zero);

        await scheduler.DispatchChatMessageStoredAsync(
            dbContext,
            userId,
            "telegram",
            "!room:matrix.localhost",
            messageId,
            "$evt-1",
            sentAt,
            CancellationToken.None);

        await routing.Received(1).Defer(
            "superchat-pipeline",
            ConversationWindowSettlement.SettleDelay,
            Arg.Is<ProcessConversationAfterSettleCommand>(command =>
                command.UserId == userId &&
                command.ExternalChatId == "!room:matrix.localhost" &&
                command.TriggerMessageId == messageId &&
                command.TriggerExternalMessageId == "$evt-1"),
            Arg.Any<IDictionary<string, string>?>());
        await routing.Received(1).Send(
            "superchat-pipeline",
            Arg.Is<RebuildConversationChunksCommand>(command =>
                command.UserId == userId &&
                command.ExternalChatId == "!room:matrix.localhost" &&
                command.TriggerMessageId == messageId &&
                command.TriggerExternalMessageId == "$evt-1" &&
                command.RebuildFrom == sentAt.AddMinutes(-15)),
            Arg.Any<IDictionary<string, string>?>());
    }

    [Fact]
    public async Task NoOpScheduler_CompletesWithoutThrowing()
    {
        var scheduler = new NoOpPipelineCommandScheduler(
            NullLogger<NoOpPipelineCommandScheduler>.Instance);

        using var dbContext = CreateDbContext();
        await scheduler.DispatchChatMessageStoredAsync(
            dbContext,
            Guid.NewGuid(),
            "telegram",
            "!room:matrix.localhost",
            Guid.NewGuid(),
            "$evt-1",
            DateTimeOffset.UtcNow,
            CancellationToken.None);
    }

    private static SuperChatDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseInMemoryDatabase($"superchat-pipeline-scheduler-{Guid.NewGuid():N}")
            .Options;

        return new SuperChatDbContext(options);
    }
}
