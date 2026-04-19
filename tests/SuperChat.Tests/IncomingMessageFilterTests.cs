using SuperChat.Contracts.Features.Operations;
using IncomingMessageFilter = SuperChat.Infrastructure.Features.Messaging.IncomingMessageFilter;

namespace SuperChat.Tests;

public sealed class IncomingMessageFilterTests
{
    [Fact]
    public void Evaluate_RejectsMessagesFromAutomatedSenders()
    {
        var result = IncomingMessageFilter.Evaluate(
            new IncomingMessageFilterOptions(),
            "m.text",
            "Присоединяйтесь к каналу, там все детали.",
            senderIsBot: true);

        Assert.False(result.ShouldAccept);
        Assert.Equal("automated_sender", result.Reason);
    }

    [Fact]
    public void Evaluate_RejectsUnsupportedMessageTypes()
    {
        var result = IncomingMessageFilter.Evaluate(
            new IncomingMessageFilterOptions(),
            "m.image",
            "contract.png",
            senderIsBot: false);

        Assert.False(result.ShouldAccept);
        Assert.Equal("message_type", result.Reason);
    }

    [Fact]
    public void Evaluate_RejectsTelegramInviteLinks()
    {
        var result = IncomingMessageFilter.Evaluate(
            new IncomingMessageFilterOptions(),
            "m.text",
            "Заходите в чат https://t.me/+abc123xyz",
            senderIsBot: false);

        Assert.False(result.ShouldAccept);
        Assert.Equal("invite_link", result.Reason);
    }

    [Fact]
    public void Evaluate_RejectsLinkOnlyMessages()
    {
        var result = IncomingMessageFilter.Evaluate(
            new IncomingMessageFilterOptions(),
            "m.text",
            "https://example.com/lead",
            senderIsBot: false);

        Assert.False(result.ShouldAccept);
        Assert.Equal("link_only", result.Reason);
    }

    [Fact]
    public void Evaluate_AllowsWorkingMessageWithUsefulTextAndLink()
    {
        var result = IncomingMessageFilter.Evaluate(
            new IncomingMessageFilterOptions(),
            "m.text",
            "Отправил договор на согласование: https://example.com/contract",
            senderIsBot: false);

        Assert.True(result.ShouldAccept);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Evaluate_AllowsMessagesWhenFilterIsDisabled()
    {
        var result = IncomingMessageFilter.Evaluate(
            new IncomingMessageFilterOptions
            {
                Enabled = false
            },
            "m.image",
            "https://t.me/+abc123xyz",
            senderIsBot: true);

        Assert.True(result.ShouldAccept);
        Assert.Null(result.Reason);
    }
}
