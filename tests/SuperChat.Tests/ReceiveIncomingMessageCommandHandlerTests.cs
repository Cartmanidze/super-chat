using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Features.Messaging;
using SuperChat.Infrastructure.Features.Operations;

namespace SuperChat.Tests;

public sealed class ReceiveIncomingMessageCommandHandlerTests
{
    [Fact]
    public async Task Handle_PassesMessageThroughNormalizationService_WithTelegramLabel()
    {
        var store = new RecordingMessageNormalizationService();
        var handler = CreateHandler(store);

        await handler.Handle(new ReceiveIncomingMessageCommand(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ChatSourceKind.Telegram,
            "tg:chat:42",
            "tg:msg:100",
            "Alice",
            "Привет, ты на связи?",
            new DateTimeOffset(2026, 04, 19, 10, 00, 00, TimeSpan.Zero)));

        var call = Assert.Single(store.Stored);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), call.UserId);
        Assert.Equal("telegram", call.Source);
        Assert.Equal("tg:chat:42", call.ExternalChatId);
        Assert.Equal("tg:msg:100", call.ExternalMessageId);
        Assert.Equal("Alice", call.SenderName);
    }

    [Fact]
    public async Task Handle_UsesMaxLabel_WhenSourceIsMax()
    {
        var store = new RecordingMessageNormalizationService();
        var handler = CreateHandler(store);

        await handler.Handle(new ReceiveIncomingMessageCommand(
            Guid.NewGuid(),
            ChatSourceKind.Max,
            "max:chat:1",
            "max:msg:1",
            "Bob",
            "Привет",
            DateTimeOffset.UtcNow));

        var call = Assert.Single(store.Stored);
        Assert.Equal("max", call.Source);
    }

    [Fact]
    public async Task Handle_DoesNotStoreMessage_WhenFilterRejectsBody()
    {
        var store = new RecordingMessageNormalizationService();
        var handler = CreateHandler(store);

        await handler.Handle(new ReceiveIncomingMessageCommand(
            Guid.NewGuid(),
            ChatSourceKind.Telegram,
            "tg:chat:42",
            "tg:msg:forward",
            "Alice",
            "Forwarded from channel SuperBot\nПривет",
            DateTimeOffset.UtcNow));

        Assert.Empty(store.Stored);
    }

    private static ReceiveIncomingMessageCommandHandler CreateHandler(
        RecordingMessageNormalizationService store)
    {
        var filter = new IncomingMessageFilter(Options.Create(new IncomingMessageFilterOptions()));
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        return new ReceiveIncomingMessageCommandHandler(
            store,
            filter,
            lifetime,
            NullLogger<ReceiveIncomingMessageCommandHandler>.Instance);
    }

    private sealed class RecordingMessageNormalizationService : IMessageNormalizationService
    {
        public List<StoredMessage> Stored { get; } = [];

        public Task<bool> TryStoreAsync(
            Guid userId,
            string source,
            string externalChatId,
            string externalMessageId,
            string senderName,
            string text,
            DateTimeOffset sentAt,
            CancellationToken cancellationToken)
        {
            Stored.Add(new StoredMessage(userId, source, externalChatId, externalMessageId, senderName, text, sentAt));
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NormalizedMessage>>([]);

        public Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesForConversationAsync(
            Guid userId,
            string source,
            string externalChatId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NormalizedMessage>>([]);

        public Task<IReadOnlyList<NormalizedMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NormalizedMessage>>([]);

        public Task MarkProcessedAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed record StoredMessage(
        Guid UserId,
        string Source,
        string ExternalChatId,
        string ExternalMessageId,
        string SenderName,
        string Text,
        DateTimeOffset SentAt);
}
