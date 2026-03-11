using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface IApiSessionService
{
    Task<ApiSession> IssueAsync(AppUser user, CancellationToken cancellationToken);

    Task<AppUser?> GetUserAsync(string token, CancellationToken cancellationToken);

    Task RevokeAsync(string token, CancellationToken cancellationToken);
}
