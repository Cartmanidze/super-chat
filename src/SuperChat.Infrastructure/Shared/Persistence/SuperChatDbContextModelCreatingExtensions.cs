using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Infrastructure.Shared.Persistence;

internal static class SuperChatDbContextModelCreatingExtensions
{
    public static void ConfigureSuperChat(this ModelBuilder builder)
    {
        var telegramStateConverter = new EnumToStringConverter<TelegramConnectionState>();
        var extractedItemKindConverter = new EnumToStringConverter<ExtractedItemKind>();
        var meetingStatusConverter = new EnumToStringConverter<MeetingStatus>();

        builder.Entity<PilotInviteEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "pilot_invites", SuperChatConsts.DbSchema);
            b.HasKey(item => item.Email);
            b.ConfigureByConvention();
        });

        builder.Entity<AppUserEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "app_users", SuperChatConsts.DbSchema);
            b.HasKey(item => item.Id);
            b.ConfigureByConvention();
            b.HasIndex(item => item.Email).IsUnique();
            b.Property(item => item.TimeZoneId).HasMaxLength(100);
        });

        builder.Entity<VerificationCodeEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "verification_codes", SuperChatConsts.DbSchema);
            b.HasKey(item => item.Id);
            b.ConfigureByConvention();
            b.Property(item => item.Email).HasMaxLength(320);
            b.Property(item => item.CodeHash).HasMaxLength(44);
            b.Property(item => item.CodeSalt).HasMaxLength(24);
            b.Property(item => item.Consumed).IsConcurrencyToken();
            b.HasIndex(item => new { item.Email, item.CreatedAt });
        });

        builder.Entity<ApiSessionEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "api_sessions", SuperChatConsts.DbSchema);
            b.HasKey(item => item.Token);
            b.ConfigureByConvention();
            b.HasIndex(item => item.UserId);
        });

        builder.Entity<MatrixIdentityEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "matrix_identities", SuperChatConsts.DbSchema);
            b.HasKey(item => item.UserId);
            b.ConfigureByConvention();
            b.HasIndex(item => item.MatrixUserId).IsUnique();
        });

        builder.Entity<TelegramConnectionEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "telegram_connections", SuperChatConsts.DbSchema);
            b.HasKey(item => item.UserId);
            b.ConfigureByConvention();
            b.Property(item => item.State).HasConversion(telegramStateConverter);
            b.HasIndex(item => item.State);
        });

        builder.Entity<SyncCheckpointEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "sync_checkpoints", SuperChatConsts.DbSchema);
            b.HasKey(item => item.UserId);
            b.ConfigureByConvention();
        });

        builder.Entity<NormalizedMessageEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "normalized_messages", SuperChatConsts.DbSchema);
            b.HasKey(item => item.Id);
            b.ConfigureByConvention();
            b.HasIndex(item => item.Processed);
            b.HasIndex(item => new { item.UserId, item.MatrixRoomId, item.MatrixEventId }).IsUnique();
        });

        builder.Entity<ExtractedItemEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "extracted_items", SuperChatConsts.DbSchema);
            b.HasKey(item => item.Id);
            b.ConfigureByConvention();
            b.Property(item => item.Kind).HasConversion(extractedItemKindConverter);
            b.HasIndex(item => new { item.UserId, item.ObservedAt });
        });

        builder.Entity<WorkItemEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "work_items", SuperChatConsts.DbSchema);
            b.HasKey(item => item.Id);
            b.ConfigureByConvention();
            b.Property(item => item.Kind).HasConversion(extractedItemKindConverter);
            b.HasIndex(item => new { item.UserId, item.ObservedAt });
            b.HasIndex(item => new { item.UserId, item.DueAt });
        });

        builder.Entity<MeetingEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "meetings", SuperChatConsts.DbSchema);
            b.HasKey(item => item.Id);
            b.ConfigureByConvention();
            b.Property(item => item.Status).HasConversion(meetingStatusConverter);
            b.HasIndex(item => new { item.UserId, item.ScheduledFor });
            b.HasIndex(item => new { item.UserId, item.Status, item.ScheduledFor });
            b.HasIndex(item => new { item.UserId, item.SourceEventId }).IsUnique();
        });

        builder.Entity<FeedbackEventEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "feedback_events", SuperChatConsts.DbSchema);
            b.HasKey(item => item.Id);
            b.ConfigureByConvention();
            b.HasIndex(item => new { item.UserId, item.CreatedAt });
        });

        builder.Entity<ChunkBuildCheckpointEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "chunk_build_checkpoints", SuperChatConsts.DbSchema);
            b.HasKey(item => item.UserId);
            b.ConfigureByConvention();
        });

        builder.Entity<MeetingProjectionCheckpointEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "meeting_projection_checkpoints", SuperChatConsts.DbSchema);
            b.HasKey(item => item.UserId);
            b.ConfigureByConvention();
        });

        builder.Entity<MessageChunkEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "message_chunks", SuperChatConsts.DbSchema);
            b.HasKey(item => item.Id);
            b.ConfigureByConvention();
            b.HasIndex(item => new { item.UserId, item.TsFrom });
            b.HasIndex(item => new { item.UserId, item.ChatId, item.TsFrom });
        });

        builder.Entity<RetrievalLogEntity>(b =>
        {
            b.ToTable(SuperChatConsts.DbTablePrefix + "retrieval_logs", SuperChatConsts.DbSchema);
            b.HasKey(item => item.Id);
            b.ConfigureByConvention();
            b.HasIndex(item => new { item.UserId, item.CreatedAt });
        });
    }
}
