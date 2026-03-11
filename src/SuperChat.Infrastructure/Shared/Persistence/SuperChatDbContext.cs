using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Persistence;

public sealed class SuperChatDbContext(DbContextOptions<SuperChatDbContext> options) : DbContext(options)
{
    public DbSet<PilotInviteEntity> PilotInvites => Set<PilotInviteEntity>();

    public DbSet<AppUserEntity> AppUsers => Set<AppUserEntity>();

    public DbSet<MagicLinkTokenEntity> MagicLinks => Set<MagicLinkTokenEntity>();

    public DbSet<ApiSessionEntity> ApiSessions => Set<ApiSessionEntity>();

    public DbSet<MatrixIdentityEntity> MatrixIdentities => Set<MatrixIdentityEntity>();

    public DbSet<TelegramConnectionEntity> TelegramConnections => Set<TelegramConnectionEntity>();

    public DbSet<NormalizedMessageEntity> NormalizedMessages => Set<NormalizedMessageEntity>();

    public DbSet<ExtractedItemEntity> ExtractedItems => Set<ExtractedItemEntity>();

    public DbSet<FeedbackEventEntity> FeedbackEvents => Set<FeedbackEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var telegramStateConverter = new EnumToStringConverter<TelegramConnectionState>();
        var extractedItemKindConverter = new EnumToStringConverter<ExtractedItemKind>();

        modelBuilder.Entity<PilotInviteEntity>(entity =>
        {
            entity.ToTable("pilot_invites");
            entity.HasKey(item => item.Email);
            entity.Property(item => item.Email).HasColumnName("email");
            entity.Property(item => item.InvitedBy).HasColumnName("invited_by");
            entity.Property(item => item.InvitedAt).HasColumnName("invited_at");
            entity.Property(item => item.IsActive).HasColumnName("is_active");
        });

        modelBuilder.Entity<AppUserEntity>(entity =>
        {
            entity.ToTable("app_users");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Email).HasColumnName("email");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.LastSeenAt).HasColumnName("last_seen_at");
            entity.HasIndex(item => item.Email).IsUnique();
        });

        modelBuilder.Entity<MagicLinkTokenEntity>(entity =>
        {
            entity.ToTable("magic_links");
            entity.HasKey(item => item.Value);
            entity.Property(item => item.Value).HasColumnName("value");
            entity.Property(item => item.Email).HasColumnName("email");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.ExpiresAt).HasColumnName("expires_at");
            entity.Property(item => item.Consumed).HasColumnName("consumed");
            entity.Property(item => item.ConsumedByUserId).HasColumnName("consumed_by_user_id");
            entity.HasIndex(item => new { item.Email, item.CreatedAt });
        });

        modelBuilder.Entity<ApiSessionEntity>(entity =>
        {
            entity.ToTable("api_sessions");
            entity.HasKey(item => item.Token);
            entity.Property(item => item.Token).HasColumnName("token");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.ExpiresAt).HasColumnName("expires_at");
            entity.HasIndex(item => item.UserId);
        });

        modelBuilder.Entity<MatrixIdentityEntity>(entity =>
        {
            entity.ToTable("matrix_identities");
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.MatrixUserId).HasColumnName("matrix_user_id");
            entity.Property(item => item.AccessToken).HasColumnName("access_token");
            entity.Property(item => item.ProvisionedAt).HasColumnName("provisioned_at");
            entity.HasIndex(item => item.MatrixUserId).IsUnique();
        });

        modelBuilder.Entity<TelegramConnectionEntity>(entity =>
        {
            entity.ToTable("telegram_connections");
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.State).HasColumnName("state").HasConversion(telegramStateConverter);
            entity.Property(item => item.WebLoginUrl).HasColumnName("web_login_url");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Property(item => item.LastSyncedAt).HasColumnName("last_synced_at");
            entity.Property(item => item.DevelopmentSeededAt).HasColumnName("development_seeded_at");
            entity.HasIndex(item => item.State);
        });

        modelBuilder.Entity<NormalizedMessageEntity>(entity =>
        {
            entity.ToTable("normalized_messages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Source).HasColumnName("source");
            entity.Property(item => item.MatrixRoomId).HasColumnName("matrix_room_id");
            entity.Property(item => item.MatrixEventId).HasColumnName("matrix_event_id");
            entity.Property(item => item.SenderName).HasColumnName("sender_name");
            entity.Property(item => item.Text).HasColumnName("text");
            entity.Property(item => item.SentAt).HasColumnName("sent_at");
            entity.Property(item => item.IngestedAt).HasColumnName("ingested_at");
            entity.Property(item => item.Processed).HasColumnName("processed");
            entity.HasIndex(item => item.Processed);
            entity.HasIndex(item => new { item.UserId, item.MatrixRoomId, item.MatrixEventId }).IsUnique();
        });

        modelBuilder.Entity<ExtractedItemEntity>(entity =>
        {
            entity.ToTable("extracted_items");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Kind).HasColumnName("kind").HasConversion(extractedItemKindConverter);
            entity.Property(item => item.Title).HasColumnName("title");
            entity.Property(item => item.Summary).HasColumnName("summary");
            entity.Property(item => item.SourceRoom).HasColumnName("source_room");
            entity.Property(item => item.SourceEventId).HasColumnName("source_event_id");
            entity.Property(item => item.Person).HasColumnName("person");
            entity.Property(item => item.ObservedAt).HasColumnName("observed_at");
            entity.Property(item => item.DueAt).HasColumnName("due_at");
            entity.Property(item => item.Confidence).HasColumnName("confidence");
            entity.HasIndex(item => new { item.UserId, item.ObservedAt });
        });

        modelBuilder.Entity<FeedbackEventEntity>(entity =>
        {
            entity.ToTable("feedback_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Area).HasColumnName("area");
            entity.Property(item => item.Value).HasColumnName("value");
            entity.Property(item => item.Notes).HasColumnName("notes");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(item => new { item.UserId, item.CreatedAt });
        });
    }
}

public sealed class PilotInviteEntity
{
    public string Email { get; set; } = string.Empty;
    public string InvitedBy { get; set; } = string.Empty;
    public DateTimeOffset InvitedAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AppUserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}

public sealed class MagicLinkTokenEntity
{
    public string Value { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Consumed { get; set; }
    public Guid? ConsumedByUserId { get; set; }
}

public sealed class ApiSessionEntity
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class MatrixIdentityEntity
{
    public Guid UserId { get; set; }
    public string MatrixUserId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ProvisionedAt { get; set; }
}

public sealed class TelegramConnectionEntity
{
    public Guid UserId { get; set; }
    public TelegramConnectionState State { get; set; }
    public string? WebLoginUrl { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset? DevelopmentSeededAt { get; set; }
}

public sealed class NormalizedMessageEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string MatrixRoomId { get; set; } = string.Empty;
    public string MatrixEventId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset IngestedAt { get; set; }
    public bool Processed { get; set; }
}

public sealed class ExtractedItemEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ExtractedItemKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SourceRoom { get; set; } = string.Empty;
    public string SourceEventId { get; set; } = string.Empty;
    public string? Person { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public double Confidence { get; set; }
}

public sealed class FeedbackEventEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Area { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
