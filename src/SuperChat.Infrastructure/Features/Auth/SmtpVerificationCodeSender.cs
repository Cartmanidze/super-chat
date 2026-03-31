using System.Net;
using System.Net.Mail;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Auth;

namespace SuperChat.Infrastructure.Features.Auth;

public sealed class SmtpVerificationCodeSender(
    EmailOptions emailOptions,
    PilotOptions pilotOptions,
    ILogger<SmtpVerificationCodeSender> logger) : IVerificationCodeSender
{
    public async Task SendAsync(string email, string code, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(email);
        Guard.Against.NullOrWhiteSpace(code);

        var expiryMinutes = pilotOptions.VerificationCodeMinutes;

        var message = new MailMessage
        {
            From = new MailAddress(emailOptions.FromSender),
            Subject = $"Super Chat — код подтверждения: {code}",
            Body = $"Ваш код подтверждения: {code}\n\nКод действителен {expiryMinutes} минут. Если вы не запрашивали вход, проигнорируйте это письмо.",
            IsBodyHtml = false
        };
        message.To.Add(email);

        try
        {
            using var client = new SmtpClient(emailOptions.SmtpHost, emailOptions.SmtpPort)
            {
                EnableSsl = emailOptions.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(emailOptions.SmtpUsername))
            {
                client.Credentials = new NetworkCredential(emailOptions.SmtpUsername, emailOptions.SmtpPassword);
            }

            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Verification code email sent to {Email} via {Host}:{Port}", email, emailOptions.SmtpHost, emailOptions.SmtpPort);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification code email to {Email} via {Host}:{Port}", email, emailOptions.SmtpHost, emailOptions.SmtpPort);
            throw;
        }
    }
}
