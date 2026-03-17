namespace SuperChat.Web.Security;

public interface IAdminPasswordService
{
    bool IsConfigured { get; }

    bool Verify(string password);
}
