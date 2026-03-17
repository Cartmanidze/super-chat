using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using SuperChat.Contracts.Features.Admin;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public sealed class PilotInviteAdminService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory) : IPilotInviteAdminService
{
    public async Task<IReadOnlyList<AdminInviteViewModel>> GetInvitesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var invites = await dbContext.PilotInvites
            .AsNoTracking()
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.Email)
            .Select(item => item.ToAdminInviteViewModel())
            .ToListAsync(cancellationToken);

        return invites;
    }

    public async Task<AdminInviteMutationResult> AddInviteAsync(
        string email,
        string invitedBy,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (!LooksLikeValidEmail(normalizedEmail))
        {
            return new AdminInviteMutationResult(false, "Введите корректный email.");
        }

        var normalizedInvitedBy = string.IsNullOrWhiteSpace(invitedBy)
            ? "admin"
            : invitedBy.Trim().ToLowerInvariant();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingInvite = await dbContext.PilotInvites
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        if (existingInvite is not null)
        {
            if (existingInvite.IsActive)
            {
                return new AdminInviteMutationResult(true, "Этот email уже есть в allowlist.");
            }

            existingInvite.IsActive = true;
            existingInvite.InvitedBy = normalizedInvitedBy;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new AdminInviteMutationResult(true, "Доступ для email снова включён.");
        }

        dbContext.PilotInvites.Add(new PilotInviteEntity
        {
            Email = normalizedEmail,
            InvitedBy = normalizedInvitedBy,
            InvitedAt = DateTimeOffset.UtcNow,
            IsActive = true
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdminInviteMutationResult(true, "Email добавлен в allowlist.");
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static bool LooksLikeValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(email);
            return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
