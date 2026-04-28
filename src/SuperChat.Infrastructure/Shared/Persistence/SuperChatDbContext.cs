using Microsoft.EntityFrameworkCore;

namespace SuperChat.Infrastructure.Shared.Persistence;

public sealed class SuperChatDbContext(DbContextOptions<SuperChatDbContext> options) : DbContext(options)
{
    internal DbSet<PilotInviteEntity> PilotInvites => Set<PilotInviteEntity>();

    internal DbSet<AppUserEntity> AppUsers => Set<AppUserEntity>();

    internal DbSet<VerificationCodeEntity> VerificationCodes => Set<VerificationCodeEntity>();

    internal DbSet<ApiSessionEntity> ApiSessions => Set<ApiSessionEntity>();

    internal DbSet<TelegramConnectionEntity> TelegramConnections => Set<TelegramConnectionEntity>();

    internal DbSet<TelegramSessionEntity> TelegramSessions => Set<TelegramSessionEntity>();

    internal DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();

    internal DbSet<ExtractedItemEntity> ExtractedItems => Set<ExtractedItemEntity>();

    internal DbSet<WorkItemEntity> WorkItems => Set<WorkItemEntity>();

    internal DbSet<MeetingEntity> Meetings => Set<MeetingEntity>();

    internal DbSet<FeedbackEventEntity> FeedbackEvents => Set<FeedbackEventEntity>();

    internal DbSet<ChunkBuildCheckpointEntity> ChunkBuildCheckpoints => Set<ChunkBuildCheckpointEntity>();

    internal DbSet<MeetingProjectionCheckpointEntity> MeetingProjectionCheckpoints => Set<MeetingProjectionCheckpointEntity>();

    internal DbSet<MessageChunkEntity> MessageChunks => Set<MessageChunkEntity>();

    internal DbSet<RetrievalLogEntity> RetrievalLogs => Set<RetrievalLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureSuperChat();
    }
}
