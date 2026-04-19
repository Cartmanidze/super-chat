using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Operations;

namespace SuperChat.Infrastructure.Features.Messaging;

public sealed partial class IncomingMessageFilter(IOptions<IncomingMessageFilterOptions> options)
{
    internal IncomingMessageFilterResult Evaluate(string messageType, string body, bool? senderIsBot)
    {
        return Evaluate(options.Value, messageType, body, senderIsBot);
    }

    internal static IncomingMessageFilterResult Evaluate(
        IncomingMessageFilterOptions options,
        string messageType,
        string body,
        bool? senderIsBot)
    {
        if (!options.Enabled)
        {
            return IncomingMessageFilterResult.Allow;
        }

        if (!ShouldAcceptMessageBody(body))
        {
            return IncomingMessageFilterResult.Reject("blocked_body");
        }

        if (!IsAllowedMessageType(options, messageType))
        {
            return IncomingMessageFilterResult.Reject("message_type");
        }

        if (senderIsBot == true)
        {
            return IncomingMessageFilterResult.Reject("automated_sender");
        }

        if (ContainsInviteLink(body, options.InviteLinkFragments))
        {
            return IncomingMessageFilterResult.Reject("invite_link");
        }

        if (HasTooManyUrls(body, options.MaxAllowedUrls))
        {
            return IncomingMessageFilterResult.Reject("too_many_urls");
        }

        if (LooksLikeLinkOnlyMessage(body, options.MinTextCharactersWhenLinksPresent))
        {
            return IncomingMessageFilterResult.Reject("link_only");
        }

        return IncomingMessageFilterResult.Allow;
    }

    internal static bool ShouldAcceptMessageBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var normalized = body.TrimStart();
        return !normalized.StartsWith("Forwarded from channel ", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("Переслано из канала ", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAllowedMessageType(IncomingMessageFilterOptions options, string messageType)
    {
        var allowedTypes = options.AllowedMessageTypes ?? [];
        if (allowedTypes.Length == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(messageType))
        {
            return false;
        }

        var normalizedMessageType = messageType.Trim();
        return allowedTypes.Any(
            allowedType => !string.IsNullOrWhiteSpace(allowedType) &&
                           string.Equals(allowedType.Trim(), normalizedMessageType, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool ContainsInviteLink(string body, string[] inviteLinkFragments)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var fragments = inviteLinkFragments ?? [];
        return fragments.Any(
            fragment => !string.IsNullOrWhiteSpace(fragment) &&
                        body.Contains(fragment.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    internal static bool HasTooManyUrls(string body, int maxAllowedUrls)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        return UrlRegex().Matches(body).Count > Math.Max(0, maxAllowedUrls);
    }

    internal static bool LooksLikeLinkOnlyMessage(string body, int minTextCharactersWhenLinksPresent)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var urls = UrlRegex().Matches(body);
        if (urls.Count == 0)
        {
            return false;
        }

        var textWithoutUrls = UrlRegex().Replace(body, " ");
        var remainingTextCharacters = textWithoutUrls.Count(char.IsLetterOrDigit);
        return remainingTextCharacters < Math.Max(0, minTextCharactersWhenLinksPresent);
    }

    [GeneratedRegex(@"(?:https?://|(?:t|telegram)\.me/)\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UrlRegex();
}

internal readonly record struct IncomingMessageFilterResult(bool ShouldAccept, string? Reason)
{
    internal static IncomingMessageFilterResult Allow { get; } = new(true, null);

    internal static IncomingMessageFilterResult Reject(string reason) => new(false, reason);
}
