namespace SuperChat.Contracts.Features.Admin;

public interface IPilotInviteAdminService
{
    Task<IReadOnlyList<AdminInviteViewModel>> GetInvitesAsync(CancellationToken cancellationToken);

    Task<AdminInviteMutationResult> AddInviteAsync(string email, string invitedBy, CancellationToken cancellationToken);
}
