using System.Security.Cryptography;
using SuperChat.Domain.Abstractions;
using SuperChat.Domain.Model;

namespace SuperChat.Domain.Services;

public sealed class MagicLinkIssuer : IMagicLinkIssuer
{
    private readonly TimeSpan _lifetime;

    public MagicLinkIssuer(TimeSpan lifetime)
    {
        _lifetime = lifetime;
    }

    public MagicLinkToken Issue(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        return new MagicLinkToken(
            Value: CreateToken(),
            Email: normalizedEmail,
            CreatedAt: now,
            ExpiresAt: now.Add(_lifetime),
            Consumed: false,
            ConsumedByUserId: null);
    }

    public bool IsUsable(MagicLinkToken token, DateTimeOffset now)
    {
        return !token.Consumed && token.ExpiresAt > now;
    }

    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
