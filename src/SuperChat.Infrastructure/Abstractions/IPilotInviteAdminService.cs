using SuperChat.Contracts.Features.Admin;

namespace SuperChat.Infrastructure.Abstractions;

public interface IPilotInviteAdminService
{
    Task<IReadOnlyList<AdminInviteViewModel>> GetInvitesAsync(CancellationToken cancellationToken);

    Task<AdminInviteMutationResult> AddInviteAsync(string email, string invitedBy, CancellationToken cancellationToken);
}
