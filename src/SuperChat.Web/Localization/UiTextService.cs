using Microsoft.Extensions.Localization;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Web.Localization;

public sealed class UiTextService(IStringLocalizer<SharedResource> localizer) : IUiTextService
{
    public string Kind(string kind)
    {
        return kind switch
        {
            "Task" => localizer["Kind.Task"],
            "Meeting" => localizer["Kind.Meeting"],
            "Commitment" => localizer["Kind.Commitment"],
            "WaitingOn" => localizer["Kind.WaitingOn"],
            "Message" => localizer["Kind.Message"],
            _ => kind
        };
    }

    public string CardTitle(string title)
    {
        return title switch
        {
            "Action needed" => localizer["CardTitle.ActionNeeded"],
            "Upcoming meeting" => localizer["CardTitle.UpcomingMeeting"],
            "Commitment made" => localizer["CardTitle.CommitmentMade"],
            "Awaiting response" => localizer["CardTitle.AwaitingResponse"],
            "Follow-up candidate" => localizer["CardTitle.FollowUpCandidate"],
            _ => title
        };
    }

    public string ConnectionState(string state)
    {
        return state switch
        {
            "NotStarted" => localizer["TelegramState.NotStarted"],
            "BridgePending" => localizer["TelegramState.BridgePending"],
            "Connected" => localizer["TelegramState.Connected"],
            "RequiresSetup" => localizer["TelegramState.RequiresSetup"],
            "Disconnected" => localizer["TelegramState.Disconnected"],
            "Error" => localizer["TelegramState.Error"],
            _ => state
        };
    }

    public string SourceRoom(string sourceRoom)
    {
        if (string.IsNullOrWhiteSpace(sourceRoom))
        {
            return localizer["SourceRoom.UnknownChat"];
        }

        return LooksLikeMatrixRoomId(sourceRoom)
            ? localizer["SourceRoom.TelegramChat"]
            : sourceRoom;
    }

    public string MagicLinkRequestStatusText(MagicLinkRequestStatus status)
    {
        return status switch
        {
            MagicLinkRequestStatus.Created => localizer["Auth.MagicLink.Created"],
            MagicLinkRequestStatus.NotInvited => localizer["Auth.MagicLink.NotInvited"],
            _ => localizer["Auth.MagicLink.Unknown"]
        };
    }

    public string AuthVerificationStatusText(AuthVerificationStatus status)
    {
        return status switch
        {
            AuthVerificationStatus.Success => localizer["Auth.Verify.Success"],
            AuthVerificationStatus.InvalidOrExpired => localizer["Auth.Verify.InvalidOrExpired"],
            _ => localizer["Auth.Verify.Unknown"]
        };
    }

    private static bool LooksLikeMatrixRoomId(string value)
    {
        return value.StartsWith("!", StringComparison.Ordinal) && value.Contains(':', StringComparison.Ordinal);
    }
}
