namespace SuperChat.Domain.Features.Auth;

public interface IAppUserRepository
{
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<AppUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AppUser> CreateOrRefreshAsync(string email, DateTimeOffset now, CancellationToken cancellationToken);
}
