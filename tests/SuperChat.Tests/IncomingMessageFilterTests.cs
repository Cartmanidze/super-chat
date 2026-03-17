using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class IncomingMessageFilterTests
{
    [Fact]
    public void Evaluate_RejectsMessagesFromAutomatedSenders()
    {
        var result = IncomingMessageFilter.Evaluate(
            new MessageIngestionFilterOptions(),
            "m.text",
            "Присоединяйтесь к каналу, там все детали.",
            senderIsBot: true);

        Assert.False(result.ShouldIngest);
        Assert.Equal("automated_sender", result.Reason);
    }

    [Fact]
    public void Evaluate_RejectsUnsupportedMessageTypes()
    {
        var result = IncomingMessageFilter.Evaluate(
            new MessageIngestionFilterOptions(),
            "m.image",
            "contract.png",
            senderIsBot: false);

        Assert.False(result.ShouldIngest);
        Assert.Equal("message_type", result.Reason);
    }

    [Fact]
    public void Evaluate_RejectsTelegramInviteLinks()
    {
        var result = IncomingMessageFilter.Evaluate(
            new MessageIngestionFilterOptions(),
            "m.text",
            "Заходите в чат https://t.me/+abc123xyz",
            senderIsBot: false);

        Assert.False(result.ShouldIngest);
        Assert.Equal("invite_link", result.Reason);
    }

    [Fact]
    public void Evaluate_RejectsLinkOnlyMessages()
    {
        var result = IncomingMessageFilter.Evaluate(
            new MessageIngestionFilterOptions(),
            "m.text",
            "https://example.com/lead",
            senderIsBot: false);

        Assert.False(result.ShouldIngest);
        Assert.Equal("link_only", result.Reason);
    }

    [Fact]
    public void Evaluate_AllowsWorkingMessageWithUsefulTextAndLink()
    {
        var result = IncomingMessageFilter.Evaluate(
            new MessageIngestionFilterOptions(),
            "m.text",
            "Отправил договор на согласование: https://example.com/contract",
            senderIsBot: false);

        Assert.True(result.ShouldIngest);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Evaluate_AllowsMessagesWhenFilterIsDisabled()
    {
        var result = IncomingMessageFilter.Evaluate(
            new MessageIngestionFilterOptions
            {
                Enabled = false
            },
            "m.image",
            "https://t.me/+abc123xyz",
            senderIsBot: true);

        Assert.True(result.ShouldIngest);
        Assert.Null(result.Reason);
    }
}
