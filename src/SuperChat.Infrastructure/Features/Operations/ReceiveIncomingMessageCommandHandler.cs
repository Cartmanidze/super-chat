using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Infrastructure.Features.Messaging;

namespace SuperChat.Infrastructure.Features.Operations;

internal sealed class ReceiveIncomingMessageCommandHandler(
    IChatMessageStore normalizationService,
    IncomingMessageFilter filter,
    IHostApplicationLifetime applicationLifetime,
    ILogger<ReceiveIncomingMessageCommandHandler> logger) : IHandleMessages<ReceiveIncomingMessageCommand>
{
    public async Task Handle(ReceiveIncomingMessageCommand message)
    {
        var cancellationToken = applicationLifetime.ApplicationStopping;

        var filterResult = filter.Evaluate("m.text", message.Text, senderIsBot: null);
        if (!filterResult.ShouldAccept)
        {
            logger.LogInformation(
                "Rejected incoming chat message by filter. Source={Source}, ExternalChatId={ExternalChatId}, ExternalMessageId={ExternalMessageId}, Reason={Reason}.",
                message.Source,
                message.ExternalChatId,
                message.ExternalMessageId,
                filterResult.Reason);
            return;
        }

        await normalizationService.TryStoreAsync(
            message.UserId,
            message.Source.ToSourceLabel(),
            message.ExternalChatId,
            message.ExternalMessageId,
            message.SenderName,
            message.Text,
            message.SentAt,
            cancellationToken);
    }
}
