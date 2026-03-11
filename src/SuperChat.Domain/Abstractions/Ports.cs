using SuperChat.Domain.Model;

namespace SuperChat.Domain.Abstractions;

public interface IMagicLinkIssuer
{
    MagicLinkToken Issue(string email);
    bool IsUsable(MagicLinkToken token, DateTimeOffset now);
}

public interface IEmailSender
{
    Task SendMagicLinkAsync(string email, string link, CancellationToken cancellationToken);
}

public interface IMatrixProvisioningService
{
    Task<MatrixIdentity> EnsureProvisionedAsync(AppUser user, CancellationToken cancellationToken);
}

public interface ITelegramConnectionService
{
    Task<TelegramConnection> BeginConnectionAsync(AppUser user, CancellationToken cancellationToken);
    Task<TelegramConnection> CompleteDevelopmentConnectionAsync(AppUser user, CancellationToken cancellationToken);
    Task DisconnectAsync(AppUser user, CancellationToken cancellationToken);
}

public interface IMatrixSyncService
{
    Task PollAsync(CancellationToken cancellationToken);
}

public interface IAiStructuredExtractionService
{
    Task<IReadOnlyList<ExtractedItem>> ExtractAsync(
        AppUser user,
        IReadOnlyCollection<NormalizedMessage> messages,
        CancellationToken cancellationToken);
}

public interface IDigestService
{
    Task<IReadOnlyList<ExtractedItem>> GetTodayAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExtractedItem>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExtractedItem>> SearchAsync(Guid userId, string query, CancellationToken cancellationToken);
}
