using SuperChat.Domain.Features.Auth;

namespace SuperChat.Infrastructure.Features.Auth;

public interface IApiSessionService
{
    Task<ApiSession> IssueAsync(AppUser user, CancellationToken cancellationToken);

    Task<AppUser?> GetUserAsync(string token, CancellationToken cancellationToken);

    Task RevokeAsync(string token, CancellationToken cancellationToken);
}
