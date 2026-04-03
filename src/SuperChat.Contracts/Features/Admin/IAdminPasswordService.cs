namespace SuperChat.Contracts.Features.Admin;

public interface IAdminPasswordService
{
    bool IsConfigured { get; }

    bool Verify(string password);
}
