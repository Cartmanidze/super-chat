using SuperChat.Domain.Features.Intelligence;
using SuperChat.Domain.Features.Messaging;

namespace SuperChat.Tests;

public sealed class ConversationWindowSettlementTests
{
    [Fact]
    public void BuildReadyConversationWindows_GroupsMessagesIntoThreeMinuteDialogs()
    {
        var userId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 03, 16, 10, 00, 00, TimeSpan.Zero);
        var messages = new[]
        {
            CreateMessage(userId, "!room:matrix.localhost", "$1", "Alex", "Первое сообщение", baseTime, baseTime),
            CreateMessage(userId, "!room:matrix.localhost", "$2", "You", "Второе сообщение", baseTime.AddMinutes(1), baseTime.AddMinutes(1)),
            CreateMessage(userId, "!room:matrix.localhost", "$3", "Alex", "Третье сообщение", baseTime.AddMinutes(7), baseTime.AddMinutes(7))
        };

        var windows = ConversationWindowSettlement.BuildReadyConversationWindows(
            messages,
            baseTime.AddMinutes(8));

        Assert.Equal(2, windows.Count);
        Assert.Equal(2, windows[0].Messages.Count);
        Assert.Equal("$2", windows[0].LastMessage.ExternalMessageId);
        Assert.Single(windows[1].Messages);
        Assert.Equal("$3", windows[1].LastMessage.ExternalMessageId);
    }

    [Fact]
    public void BuildReadyConversationWindows_WaitsForShortSettleDelay()
    {
        var userId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 03, 16, 10, 00, 00, TimeSpan.Zero);
        var message = CreateMessage(
            userId,
            "!room:matrix.localhost",
            "$1",
            "Alex",
            "Ещё пишу",
            baseTime,
            baseTime);

        var windows = ConversationWindowSettlement.BuildReadyConversationWindows(
            [message],
            baseTime.AddSeconds(10));

        Assert.Empty(windows);
    }

    private static NormalizedMessage CreateMessage(
        Guid userId,
        string roomId,
        string eventId,
        string senderName,
        string text,
        DateTimeOffset sentAt,
        DateTimeOffset receivedAt)
    {
        return new NormalizedMessage(
            Guid.NewGuid(),
            userId,
            "telegram",
            roomId,
            eventId,
            senderName,
            text,
            sentAt,
            receivedAt,
            false);
    }
}
