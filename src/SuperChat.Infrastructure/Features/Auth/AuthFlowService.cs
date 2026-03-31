using System.Security.Cryptography;
using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations.Matrix;
using SuperChat.Domain.Features.Auth;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Auth;

public sealed class AuthFlowService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IMatrixProvisioningService matrixProvisioningService,
    IVerificationCodeSender codeSender,
    PilotOptions pilotOptions,
    TimeProvider timeProvider,
    ILogger<AuthFlowService> logger) : IAuthFlowService
{
    private const int RateLimitWindowMinutes = 10;
    private const int MaxCodesPerWindow = 3;

    public async Task<AppUser?> FindUserAsync(string email, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(email);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedEmail = new Email(email).Value;

        var entity = await dbContext.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<SendCodeResult> SendCodeAsync(string email, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(email);

        var normalizedEmail = new Email(email).Value;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var isAllowedInvite = await dbContext.PilotInvites
            .AsNoTracking()
            .AnyAsync(item => item.Email == normalizedEmail && item.IsActive, cancellationToken);
        var isConfiguredAdmin = pilotOptions.AdminEmails.Any(candidate =>
            !string.IsNullOrWhiteSpace(candidate) &&
            string.Equals(candidate.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase));

        if (!isAllowedInvite && !isConfiguredAdmin)
        {
            logger.LogWarning("Auth send-code rejected: email {Email} is not invited", normalizedEmail);
            return new SendCodeResult(SendCodeStatus.NotInvited, "This email is not invited to the pilot yet.");
        }

        var now = timeProvider.GetUtcNow();
        var windowStart = now.AddMinutes(-RateLimitWindowMinutes);

        var recentCodes = await dbContext.VerificationCodes
            .Where(item => item.Email == normalizedEmail)
            .Select(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var recentCodeCount = recentCodes.Count(ts => ts >= windowStart);

        if (recentCodeCount >= MaxCodesPerWindow)
        {
            logger.LogWarning("Auth send-code rate limited: email {Email}, {Count} codes in window", normalizedEmail, recentCodeCount);
            return new SendCodeResult(SendCodeStatus.TooManyRequests, "Too many code requests. Please wait before trying again.");
        }

        // Invalidate all previous unconsumed codes for this email
        var previousCodes = await dbContext.VerificationCodes
            .Where(item => item.Email == normalizedEmail && !item.Consumed)
            .ToListAsync(cancellationToken);
        foreach (var prev in previousCodes)
        {
            prev.Consumed = true;
        }

        var code = GenerateCode();
        var salt = GenerateSalt();
        var entity = new VerificationCodeEntity
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            CodeHash = HashCode(code, salt),
            CodeSalt = Convert.ToBase64String(salt),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(pilotOptions.VerificationCodeMinutes),
            Consumed = false,
            FailedAttempts = 0
        };

        dbContext.VerificationCodes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await codeSender.SendAsync(normalizedEmail, code, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification code to {Email} — rolling back code {CodeId}", normalizedEmail, entity.Id);
            entity.Consumed = true;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return new SendCodeResult(SendCodeStatus.DeliveryFailed, "Could not send verification email. Please try again.");
        }

        logger.LogInformation("Auth verification code sent to {Email}", normalizedEmail);
        return new SendCodeResult(SendCodeStatus.Sent, "Verification code sent.");
    }

    public async Task<AuthVerificationResult> VerifyCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(email);
        Guard.Against.NullOrWhiteSpace(code);

        var normalizedEmail = new Email(email).Value;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var candidates = await dbContext.VerificationCodes
            .Where(item => item.Email == normalizedEmail && !item.Consumed)
            .ToListAsync(cancellationToken);

        var entity = candidates
            .Where(item => item.ExpiresAt > now)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();

        if (entity is null)
        {
            logger.LogWarning("Auth verify-code failed: no active code for {Email}", normalizedEmail);
            return AuthVerificationResult.Failure(AuthVerificationStatus.InvalidOrExpired, "Verification code is invalid or expired.");
        }

        if (entity.FailedAttempts >= pilotOptions.MaxVerificationAttempts)
        {
            logger.LogWarning("Auth verify-code locked out: {Email} exceeded {Max} attempts", normalizedEmail, pilotOptions.MaxVerificationAttempts);
            return AuthVerificationResult.Failure(AuthVerificationStatus.TooManyAttempts, "Too many failed attempts. Please request a new code.");
        }

        var salt = Convert.FromBase64String(entity.CodeSalt);
        var submittedHash = HashCode(code.Trim(), salt);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(entity.CodeHash),
                Convert.FromBase64String(submittedHash)))
        {
            entity.FailedAttempts++;
            await SaveChangesHandlingConcurrencyAsync(dbContext);
            logger.LogWarning("Auth verify-code wrong code: {Email}, attempt {Attempt}/{Max}", normalizedEmail, entity.FailedAttempts, pilotOptions.MaxVerificationAttempts);
            return AuthVerificationResult.Failure(AuthVerificationStatus.InvalidOrExpired, "Verification code is invalid or expired.");
        }

        var user = await CreateOrRefreshUserAsync(dbContext, normalizedEmail, now, cancellationToken);
        entity.Consumed = true;
        entity.ConsumedByUserId = user.Id;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Auth verify-code concurrency conflict for {Email} — code already consumed by another request", normalizedEmail);
            return AuthVerificationResult.Failure(AuthVerificationStatus.InvalidOrExpired, "Verification code is invalid or expired.");
        }

        try
        {
            await matrixProvisioningService.EnsureIdentityAsync(user, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Matrix provisioning failed for user {UserId} ({Email}) — auth succeeded, identity will be provisioned later", user.Id, normalizedEmail);
        }

        logger.LogInformation("Auth verification succeeded: {Email}, user {UserId}", normalizedEmail, user.Id);
        return AuthVerificationResult.Success("Signed in successfully.", user);
    }

    private static async Task SaveChangesHandlingConcurrencyAsync(SuperChatDbContext dbContext)
    {
        try
        {
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request already consumed or modified this code — ignore the counter update
        }
    }

    private static async Task<AppUser> CreateOrRefreshUserAsync(
        SuperChatDbContext dbContext,
        string normalizedEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.AppUsers.SingleOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);
        if (entity is null)
        {
            entity = new AppUserEntity
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                CreatedAt = now,
                LastSeenAt = now
            };

            dbContext.AppUsers.Add(entity);
            return entity.ToDomain();
        }

        entity.LastSeenAt = now;
        return entity.ToDomain();
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    private static byte[] GenerateSalt()
    {
        return RandomNumberGenerator.GetBytes(16);
    }

    private static string HashCode(string code, byte[] salt)
    {
        var input = new byte[salt.Length + System.Text.Encoding.UTF8.GetByteCount(code)];
        salt.CopyTo(input, 0);
        System.Text.Encoding.UTF8.GetBytes(code, input.AsSpan(salt.Length));
        return Convert.ToBase64String(SHA256.HashData(input));
    }

}
