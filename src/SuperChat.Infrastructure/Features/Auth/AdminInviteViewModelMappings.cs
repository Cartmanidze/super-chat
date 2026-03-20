using SuperChat.Contracts.Features.Admin;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Auth;

internal static class AdminInviteViewModelMappings
{
    public static AdminInviteViewModel ToAdminInviteViewModel(this PilotInviteEntity item)
    {
        return new AdminInviteViewModel(
            item.Email,
            item.InvitedBy,
            item.InvitedAt,
            item.IsActive);
    }
}
