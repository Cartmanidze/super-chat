using SuperChat.Contracts.Features.Auth;

namespace SuperChat.Web.Localization;

public interface IUiTextService
{
    string Kind(string kind);

    string CardTitle(string title);

    string ConnectionState(string state);

    string SourceRoom(string sourceRoom);

    string SendCodeStatusText(SendCodeStatus status);

    string AuthVerificationStatusText(AuthVerificationStatus status);
}
