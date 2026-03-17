using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;

namespace SuperChat.Infrastructure.Services;

public sealed partial class IncomingMessageFilter(IOptions<MessageIngestionFilterOptions> options)
{
    internal MessageIngestionFilterResult Evaluate(string messageType, string body, bool? senderIsBot)
    {
        return Evaluate(options.Value, messageType, body, senderIsBot);
    }

    internal static MessageIngestionFilterResult Evaluate(
        MessageIngestionFilterOptions options,
        string messageType,
        string body,
        bool? senderIsBot)
    {
        if (!options.Enabled)
        {
            return MessageIngestionFilterResult.Allow;
        }

        if (!ShouldIngestMessageBody(body))
        {
            return MessageIngestionFilterResult.Reject("blocked_body");
        }

        if (!IsAllowedMessageType(options, messageType))
        {
            return MessageIngestionFilterResult.Reject("message_type");
        }

        if (senderIsBot == true)
        {
            return MessageIngestionFilterResult.Reject("automated_sender");
        }

        if (ContainsInviteLink(body, options.InviteLinkFragments))
        {
            return MessageIngestionFilterResult.Reject("invite_link");
        }

        if (HasTooManyUrls(body, options.MaxAllowedUrls))
        {
            return MessageIngestionFilterResult.Reject("too_many_urls");
        }

        if (LooksLikeLinkOnlyMessage(body, options.MinTextCharactersWhenLinksPresent))
        {
            return MessageIngestionFilterResult.Reject("link_only");
        }

        return MessageIngestionFilterResult.Allow;
    }

    internal static bool ShouldIngestMessageBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var normalized = body.TrimStart();
        return !normalized.StartsWith("Forwarded from channel ", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("Переслано из канала ", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAllowedMessageType(MessageIngestionFilterOptions options, string messageType)
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

internal readonly record struct MessageIngestionFilterResult(bool ShouldIngest, string? Reason)
{
    internal static MessageIngestionFilterResult Allow { get; } = new(true, null);

    internal static MessageIngestionFilterResult Reject(string reason) => new(false, reason);
}
