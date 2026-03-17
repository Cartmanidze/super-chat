using SuperChat.Contracts.Features.Admin;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

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
