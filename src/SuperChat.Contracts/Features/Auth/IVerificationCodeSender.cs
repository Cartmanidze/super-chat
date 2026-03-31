namespace SuperChat.Contracts.Features.Auth;

public interface IVerificationCodeSender
{
    Task SendAsync(string email, string code, CancellationToken cancellationToken);
}
