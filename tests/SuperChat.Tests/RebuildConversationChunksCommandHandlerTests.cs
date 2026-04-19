using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rebus.Bus;
using Rebus.Bus.Advanced;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Infrastructure.Features.Operations;

namespace SuperChat.Tests;

public sealed class RebuildConversationChunksCommandHandlerTests
{
    [Fact]
    public async Task Handle_SchedulesMeetingProjectionBeforeChunkIndexing()
    {
        var userId = Guid.NewGuid();
        var roomId = "!room:matrix.localhost";
        var rebuildFrom = new DateTimeOffset(2026, 04, 08, 09, 00, 00, TimeSpan.Zero);
        var bus = new RecordingBus();
        var handler = new RebuildConversationChunksCommandHandler(
            new StubChunkBuilderService(new ChunkBuildRunResult(1, 1, 1, 1)),
            bus,
            Options.Create(new ChunkingOptions
            {
                Enabled = true
            }),
            new TestHostApplicationLifetime(),
            NullLogger<RebuildConversationChunksCommandHandler>.Instance);

        await handler.Handle(new RebuildConversationChunksCommand(userId, roomId, rebuildFrom));

        Assert.Collection(
            bus.SentMessages,
            item =>
            {
                var command = Assert.IsType<ProjectConversationMeetingsCommand>(item);
                Assert.Equal(userId, command.UserId);
                Assert.Equal(roomId, command.ExternalChatId);
            },
            item =>
            {
                var command = Assert.IsType<IndexConversationChunksCommand>(item);
                Assert.Equal(userId, command.UserId);
                Assert.Equal(roomId, command.ExternalChatId);
            });
    }

    private sealed class StubChunkBuilderService(ChunkBuildRunResult result) : IChunkBuilderService
    {
        public Task<ChunkBuildRunResult> BuildPendingChunksAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ChunkBuildRunResult> BuildConversationChunksAsync(
            Guid userId,
            string matrixRoomId,
            DateTimeOffset rebuildFrom,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingBus : IBus
    {
        public List<object> SentMessages { get; } = [];

        public IAdvancedApi Advanced => throw new NotSupportedException();

        public Task Send(object commandMessage, IDictionary<string, string>? optionalHeaders = null)
        {
            SentMessages.Add(commandMessage);
            return Task.CompletedTask;
        }

        public Task SendLocal(object commandMessage, IDictionary<string, string>? optionalHeaders = null)
        {
            SentMessages.Add(commandMessage);
            return Task.CompletedTask;
        }

        public Task Defer(TimeSpan delay, object message, IDictionary<string, string>? optionalHeaders = null)
        {
            throw new NotSupportedException();
        }

        public Task Defer(DateTimeOffset deferredUntil, object message, IDictionary<string, string>? optionalHeaders = null)
        {
            throw new NotSupportedException();
        }

        public Task DeferLocal(TimeSpan delay, object message, IDictionary<string, string>? optionalHeaders = null)
        {
            throw new NotSupportedException();
        }

        public Task DeferLocal(DateTimeOffset deferredUntil, object message, IDictionary<string, string>? optionalHeaders = null)
        {
            throw new NotSupportedException();
        }

        public Task Publish(object eventMessage, IDictionary<string, string>? optionalHeaders = null)
        {
            throw new NotSupportedException();
        }

        public Task Reply(object replyMessage, IDictionary<string, string>? optionalHeaders = null)
        {
            throw new NotSupportedException();
        }

        public Task Subscribe(Type eventType)
        {
            throw new NotSupportedException();
        }

        public Task Subscribe<TEvent>()
        {
            throw new NotSupportedException();
        }

        public Task Subscribe(Type eventType, string topic)
        {
            throw new NotSupportedException();
        }

        public Task Unsubscribe(Type eventType)
        {
            throw new NotSupportedException();
        }

        public Task Unsubscribe<TEvent>()
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
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
}
