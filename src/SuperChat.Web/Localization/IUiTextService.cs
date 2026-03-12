using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Web.Localization;

public interface IUiTextService
{
    string Kind(string kind);

    string CardTitle(string title);

    string ConnectionState(string state);

    string SourceRoom(string sourceRoom);

    string MagicLinkRequestStatusText(MagicLinkRequestStatus status);

    string AuthVerificationStatusText(AuthVerificationStatus status);
}
