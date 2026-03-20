using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Infrastructure.Shared.Persistence;

public sealed class SuperChatDbContext(DbContextOptions<SuperChatDbContext> options) : DbContext(options)
{
    public DbSet<PilotInviteEntity> PilotInvites => Set<PilotInviteEntity>();

    public DbSet<AppUserEntity> AppUsers => Set<AppUserEntity>();

    public DbSet<MagicLinkTokenEntity> MagicLinks => Set<MagicLinkTokenEntity>();

    public DbSet<ApiSessionEntity> ApiSessions => Set<ApiSessionEntity>();

    public DbSet<MatrixIdentityEntity> MatrixIdentities => Set<MatrixIdentityEntity>();

    public DbSet<TelegramConnectionEntity> TelegramConnections => Set<TelegramConnectionEntity>();

    public DbSet<SyncCheckpointEntity> SyncCheckpoints => Set<SyncCheckpointEntity>();

    public DbSet<NormalizedMessageEntity> NormalizedMessages => Set<NormalizedMessageEntity>();

    public DbSet<ExtractedItemEntity> ExtractedItems => Set<ExtractedItemEntity>();

    public DbSet<WorkItemEntity> WorkItems => Set<WorkItemEntity>();

    public DbSet<MeetingEntity> Meetings => Set<MeetingEntity>();

    public DbSet<FeedbackEventEntity> FeedbackEvents => Set<FeedbackEventEntity>();

    public DbSet<ChunkBuildCheckpointEntity> ChunkBuildCheckpoints => Set<ChunkBuildCheckpointEntity>();

    public DbSet<MeetingProjectionCheckpointEntity> MeetingProjectionCheckpoints => Set<MeetingProjectionCheckpointEntity>();

    public DbSet<MessageChunkEntity> MessageChunks => Set<MessageChunkEntity>();

    public DbSet<RetrievalLogEntity> RetrievalLogs => Set<RetrievalLogEntity>();

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
            entity.Property(item => item.ManagementRoomId).HasColumnName("management_room_id");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Property(item => item.LastSyncedAt).HasColumnName("last_synced_at");
            entity.Property(item => item.DevelopmentSeededAt).HasColumnName("development_seeded_at");
            entity.HasIndex(item => item.State);
        });

        modelBuilder.Entity<SyncCheckpointEntity>(entity =>
        {
            entity.ToTable("sync_checkpoints");
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.NextBatchToken).HasColumnName("next_batch_token");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
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
            entity.Property(item => item.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(item => item.ResolutionKind).HasColumnName("resolution_kind");
            entity.Property(item => item.ResolutionSource).HasColumnName("resolution_source");
            entity.HasIndex(item => new { item.UserId, item.ObservedAt });
        });

        modelBuilder.Entity<WorkItemEntity>(entity =>
        {
            entity.ToTable("work_items");
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
            entity.Property(item => item.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(item => item.ResolutionKind).HasColumnName("resolution_kind");
            entity.Property(item => item.ResolutionSource).HasColumnName("resolution_source");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(item => new { item.UserId, item.ObservedAt });
            entity.HasIndex(item => new { item.UserId, item.DueAt });
        });

        modelBuilder.Entity<MeetingEntity>(entity =>
        {
            entity.ToTable("meetings");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Title).HasColumnName("title");
            entity.Property(item => item.Summary).HasColumnName("summary");
            entity.Property(item => item.SourceRoom).HasColumnName("source_room");
            entity.Property(item => item.SourceEventId).HasColumnName("source_event_id");
            entity.Property(item => item.Person).HasColumnName("person");
            entity.Property(item => item.ObservedAt).HasColumnName("observed_at");
            entity.Property(item => item.ScheduledFor).HasColumnName("scheduled_for");
            entity.Property(item => item.Confidence).HasColumnName("confidence");
            entity.Property(item => item.MeetingProvider).HasColumnName("meeting_provider");
            entity.Property(item => item.MeetingJoinUrl).HasColumnName("meeting_join_url");
            entity.Property(item => item.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(item => item.ResolutionKind).HasColumnName("resolution_kind");
            entity.Property(item => item.ResolutionSource).HasColumnName("resolution_source");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(item => new { item.UserId, item.ScheduledFor });
            entity.HasIndex(item => new { item.UserId, item.SourceEventId }).IsUnique();
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

        modelBuilder.Entity<ChunkBuildCheckpointEntity>(entity =>
        {
            entity.ToTable("chunk_build_checkpoints");
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.LastObservedIngestedAt).HasColumnName("last_observed_ingested_at");
            entity.Property(item => item.LastObservedMessageId).HasColumnName("last_observed_message_id");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<MeetingProjectionCheckpointEntity>(entity =>
        {
            entity.ToTable("meeting_projection_checkpoints");
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.LastObservedChunkUpdatedAt).HasColumnName("last_observed_chunk_updated_at");
            entity.Property(item => item.LastObservedChunkId).HasColumnName("last_observed_chunk_id");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<MessageChunkEntity>(entity =>
        {
            entity.ToTable("message_chunks");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Source).HasColumnName("source");
            entity.Property(item => item.Provider).HasColumnName("provider");
            entity.Property(item => item.Transport).HasColumnName("transport");
            entity.Property(item => item.ChatId).HasColumnName("chat_id");
            entity.Property(item => item.PeerId).HasColumnName("peer_id");
            entity.Property(item => item.ThreadId).HasColumnName("thread_id");
            entity.Property(item => item.Kind).HasColumnName("kind");
            entity.Property(item => item.Text).HasColumnName("text");
            entity.Property(item => item.MessageCount).HasColumnName("message_count");
            entity.Property(item => item.FirstNormalizedMessageId).HasColumnName("first_normalized_message_id");
            entity.Property(item => item.LastNormalizedMessageId).HasColumnName("last_normalized_message_id");
            entity.Property(item => item.TsFrom).HasColumnName("ts_from");
            entity.Property(item => item.TsTo).HasColumnName("ts_to");
            entity.Property(item => item.ContentHash).HasColumnName("content_hash");
            entity.Property(item => item.ChunkVersion).HasColumnName("chunk_version");
            entity.Property(item => item.EmbeddingVersion).HasColumnName("embedding_version");
            entity.Property(item => item.QdrantPointId).HasColumnName("qdrant_point_id");
            entity.Property(item => item.IndexedAt).HasColumnName("indexed_at");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(item => new { item.UserId, item.TsFrom });
            entity.HasIndex(item => new { item.UserId, item.ChatId, item.TsFrom });
        });

        modelBuilder.Entity<RetrievalLogEntity>(entity =>
        {
            entity.ToTable("retrieval_logs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.QueryText).HasColumnName("query_text");
            entity.Property(item => item.QueryKind).HasColumnName("query_kind");
            entity.Property(item => item.FiltersJson).HasColumnName("filters_json");
            entity.Property(item => item.CandidateCount).HasColumnName("candidate_count");
            entity.Property(item => item.SelectedChunkIdsJson).HasColumnName("selected_chunk_ids_json");
            entity.Property(item => item.LatencyMs).HasColumnName("latency_ms");
            entity.Property(item => item.ModelVersionsJson).HasColumnName("model_versions_json");
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
    public string? ManagementRoomId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset? DevelopmentSeededAt { get; set; }
}

public sealed class SyncCheckpointEntity
{
    public Guid UserId { get; set; }
    public string? NextBatchToken { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
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
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionKind { get; set; }
    public string? ResolutionSource { get; set; }
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

public sealed class WorkItemEntity
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
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionKind { get; set; }
    public string? ResolutionSource { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class MeetingEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SourceRoom { get; set; } = string.Empty;
    public string SourceEventId { get; set; } = string.Empty;
    public string? Person { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset ScheduledFor { get; set; }
    public double Confidence { get; set; }
    public string? MeetingProvider { get; set; }
    public string? MeetingJoinUrl { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionKind { get; set; }
    public string? ResolutionSource { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ChunkBuildCheckpointEntity
{
    public Guid UserId { get; set; }
    public DateTimeOffset? LastObservedIngestedAt { get; set; }
    public Guid? LastObservedMessageId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class MeetingProjectionCheckpointEntity
{
    public Guid UserId { get; set; }
    public DateTimeOffset? LastObservedChunkUpdatedAt { get; set; }
    public Guid? LastObservedChunkId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class MessageChunkEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Transport { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public string? PeerId { get; set; }
    public string? ThreadId { get; set; }
    public string Kind { get; set; } = "dialog_chunk";
    public string Text { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public Guid? FirstNormalizedMessageId { get; set; }
    public Guid? LastNormalizedMessageId { get; set; }
    public DateTimeOffset TsFrom { get; set; }
    public DateTimeOffset TsTo { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public int ChunkVersion { get; set; } = 1;
    public string? EmbeddingVersion { get; set; }
    public string? QdrantPointId { get; set; }
    public DateTimeOffset? IndexedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RetrievalLogEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public string QueryKind { get; set; } = string.Empty;
    public string? FiltersJson { get; set; }
    public int CandidateCount { get; set; }
    public string? SelectedChunkIdsJson { get; set; }
    public int? LatencyMs { get; set; }
    public string? ModelVersionsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
