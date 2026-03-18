using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class PersistenceInitializationHostedService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IOptions<PersistenceOptions> persistenceOptions,
    IOptions<PilotOptions> pilotOptions,
    IWorkerRuntimeMonitor workerRuntimeMonitor,
    ILogger<PersistenceInitializationHostedService> logger) : IHostedService
{
    private const string WorkerKey = "persistence-initialization";
    private const string WorkerDisplayName = "Persistence Initialization";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        workerRuntimeMonitor.RegisterWorker(WorkerKey, WorkerDisplayName);
        if (!persistenceOptions.Value.AutoInitialize)
        {
            logger.LogInformation("Persistence auto-initialization is disabled.");
            workerRuntimeMonitor.MarkDisabled(WorkerKey, WorkerDisplayName, "Persistence auto-initialization is disabled.");
            return;
        }

        try
        {
            workerRuntimeMonitor.MarkRunning(WorkerKey, WorkerDisplayName);
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            await UpgradeSchemaAsync(dbContext, cancellationToken);

            var configuredInvites = pilotOptions.Value.AllowedEmails
                .Select(email => email.Trim().ToLowerInvariant())
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (configuredInvites.Count == 0)
            {
                workerRuntimeMonitor.MarkSucceeded(WorkerKey, WorkerDisplayName, "Schema is ready. No bootstrap invites configured.");
                return;
            }

            var existingInvites = await dbContext.PilotInvites
                .Where(item => configuredInvites.Contains(item.Email))
                .Select(item => item.Email)
                .ToListAsync(cancellationToken);

            var missingInvites = configuredInvites
                .Except(existingInvites, StringComparer.OrdinalIgnoreCase)
                .Select(email => new PilotInviteEntity
                {
                    Email = email,
                    InvitedBy = "bootstrap",
                    InvitedAt = DateTimeOffset.UtcNow,
                    IsActive = true
                })
                .ToList();

            if (missingInvites.Count == 0)
            {
                workerRuntimeMonitor.MarkSucceeded(WorkerKey, WorkerDisplayName, "Schema is ready. Bootstrap invites already exist.");
                return;
            }

            dbContext.PilotInvites.AddRange(missingInvites);
            await dbContext.SaveChangesAsync(cancellationToken);
            workerRuntimeMonitor.MarkSucceeded(WorkerKey, WorkerDisplayName, $"Schema is ready. Seeded {missingInvites.Count} invites.");
            logger.LogInformation("Seeded {InviteCount} pilot invites into persistence store.", missingInvites.Count);
        }
        catch (Exception exception)
        {
            workerRuntimeMonitor.MarkFailed(WorkerKey, WorkerDisplayName, exception);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static async Task UpgradeSchemaAsync(SuperChatDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE telegram_connections
            ADD COLUMN IF NOT EXISTS management_room_id text NULL;

            CREATE TABLE IF NOT EXISTS sync_checkpoints (
                user_id uuid PRIMARY KEY,
                next_batch_token text NULL,
                updated_at timestamptz NOT NULL
            );

            CREATE TABLE IF NOT EXISTS chunk_build_checkpoints (
                user_id uuid PRIMARY KEY,
                last_observed_ingested_at timestamptz NULL,
                last_observed_message_id uuid NULL,
                updated_at timestamptz NOT NULL
            );

            CREATE TABLE IF NOT EXISTS meeting_projection_checkpoints (
                user_id uuid PRIMARY KEY,
                last_observed_chunk_updated_at timestamptz NULL,
                last_observed_chunk_id uuid NULL,
                updated_at timestamptz NOT NULL
            );

            CREATE TABLE IF NOT EXISTS work_items (
                id uuid PRIMARY KEY,
                user_id uuid NOT NULL,
                kind text NOT NULL,
                title text NOT NULL,
                summary text NOT NULL,
                source_room text NOT NULL,
                source_event_id text NOT NULL,
                person text NULL,
                observed_at timestamptz NOT NULL,
                due_at timestamptz NULL,
                confidence double precision NOT NULL,
                resolved_at timestamptz NULL,
                resolution_kind text NULL,
                resolution_source text NULL,
                created_at timestamptz NOT NULL,
                updated_at timestamptz NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_work_items_user_id_observed_at
                ON work_items (user_id, observed_at);

            CREATE INDEX IF NOT EXISTS ix_work_items_user_id_due_at
                ON work_items (user_id, due_at);

            CREATE TABLE IF NOT EXISTS meetings (
                id uuid PRIMARY KEY,
                user_id uuid NOT NULL,
                title text NOT NULL,
                summary text NOT NULL,
                source_room text NOT NULL,
                source_event_id text NOT NULL,
                person text NULL,
                observed_at timestamptz NOT NULL,
                scheduled_for timestamptz NOT NULL,
                confidence double precision NOT NULL,
                meeting_provider text NULL,
                meeting_join_url text NULL,
                resolved_at timestamptz NULL,
                resolution_kind text NULL,
                resolution_source text NULL,
                created_at timestamptz NOT NULL,
                updated_at timestamptz NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_meetings_user_id_source_event_id
                ON meetings (user_id, source_event_id);

            CREATE INDEX IF NOT EXISTS ix_meetings_user_id_scheduled_for
                ON meetings (user_id, scheduled_for);

            ALTER TABLE meetings
            ADD COLUMN IF NOT EXISTS meeting_provider text NULL;

            ALTER TABLE meetings
            ADD COLUMN IF NOT EXISTS meeting_join_url text NULL;

            ALTER TABLE meetings
            ADD COLUMN IF NOT EXISTS resolved_at timestamptz NULL;

            ALTER TABLE meetings
            ADD COLUMN IF NOT EXISTS resolution_kind text NULL;

            ALTER TABLE meetings
            ADD COLUMN IF NOT EXISTS resolution_source text NULL;

            ALTER TABLE extracted_items
            ADD COLUMN IF NOT EXISTS resolved_at timestamptz NULL;

            ALTER TABLE extracted_items
            ADD COLUMN IF NOT EXISTS resolution_kind text NULL;

            ALTER TABLE extracted_items
            ADD COLUMN IF NOT EXISTS resolution_source text NULL;

            INSERT INTO work_items (
                id,
                user_id,
                kind,
                title,
                summary,
                source_room,
                source_event_id,
                person,
                observed_at,
                due_at,
                confidence,
                resolved_at,
                resolution_kind,
                resolution_source,
                created_at,
                updated_at)
            SELECT
                item.id,
                item.user_id,
                item.kind,
                item.title,
                item.summary,
                item.source_room,
                item.source_event_id,
                item.person,
                item.observed_at,
                item.due_at,
                item.confidence,
                item.resolved_at,
                item.resolution_kind,
                item.resolution_source,
                item.observed_at,
                item.observed_at
            FROM extracted_items item
            WHERE item.kind <> 'Meeting'
            ON CONFLICT (id) DO UPDATE SET
                kind = EXCLUDED.kind,
                title = EXCLUDED.title,
                summary = EXCLUDED.summary,
                source_room = EXCLUDED.source_room,
                source_event_id = EXCLUDED.source_event_id,
                person = EXCLUDED.person,
                observed_at = EXCLUDED.observed_at,
                due_at = EXCLUDED.due_at,
                confidence = EXCLUDED.confidence,
                resolved_at = EXCLUDED.resolved_at,
                resolution_kind = EXCLUDED.resolution_kind,
                resolution_source = EXCLUDED.resolution_source,
                updated_at = EXCLUDED.updated_at;

            INSERT INTO meetings (
                id,
                user_id,
                title,
                summary,
                source_room,
                source_event_id,
                person,
                observed_at,
                scheduled_for,
                confidence,
                meeting_provider,
                meeting_join_url,
                resolved_at,
                resolution_kind,
                resolution_source,
                created_at,
                updated_at)
            SELECT
                item.id,
                item.user_id,
                item.title,
                item.summary,
                item.source_room,
                item.source_event_id,
                item.person,
                item.observed_at,
                item.due_at,
                item.confidence,
                NULL,
                NULL,
                item.resolved_at,
                item.resolution_kind,
                item.resolution_source,
                item.observed_at,
                item.observed_at
            FROM extracted_items item
            WHERE item.kind = 'Meeting' AND item.due_at IS NOT NULL
            ON CONFLICT (user_id, source_event_id) DO UPDATE SET
                title = EXCLUDED.title,
                summary = EXCLUDED.summary,
                source_room = EXCLUDED.source_room,
                person = EXCLUDED.person,
                observed_at = EXCLUDED.observed_at,
                scheduled_for = EXCLUDED.scheduled_for,
                confidence = EXCLUDED.confidence,
                resolved_at = COALESCE(meetings.resolved_at, EXCLUDED.resolved_at),
                resolution_kind = COALESCE(meetings.resolution_kind, EXCLUDED.resolution_kind),
                resolution_source = COALESCE(meetings.resolution_source, EXCLUDED.resolution_source),
                updated_at = EXCLUDED.updated_at;

            DELETE FROM extracted_items
            WHERE kind <> 'Meeting';

            DELETE FROM extracted_items
            WHERE kind = 'Meeting' AND due_at IS NOT NULL;

            CREATE TABLE IF NOT EXISTS message_chunks (
                id uuid PRIMARY KEY,
                user_id uuid NOT NULL,
                source text NOT NULL,
                provider text NOT NULL,
                transport text NOT NULL,
                chat_id text NOT NULL,
                peer_id text NULL,
                thread_id text NULL,
                kind text NOT NULL,
                text text NOT NULL,
                message_count integer NOT NULL,
                first_normalized_message_id uuid NULL,
                last_normalized_message_id uuid NULL,
                ts_from timestamptz NOT NULL,
                ts_to timestamptz NOT NULL,
                content_hash text NOT NULL,
                chunk_version integer NOT NULL,
                embedding_version text NULL,
                qdrant_point_id text NULL,
                indexed_at timestamptz NULL,
                created_at timestamptz NOT NULL,
                updated_at timestamptz NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_message_chunks_user_id_ts_from
                ON message_chunks (user_id, ts_from);

            CREATE INDEX IF NOT EXISTS ix_message_chunks_user_id_chat_id_ts_from
                ON message_chunks (user_id, chat_id, ts_from);

            CREATE TABLE IF NOT EXISTS retrieval_logs (
                id uuid PRIMARY KEY,
                user_id uuid NOT NULL,
                query_text text NOT NULL,
                query_kind text NOT NULL,
                filters_json text NULL,
                candidate_count integer NOT NULL,
                selected_chunk_ids_json text NULL,
                latency_ms integer NULL,
                model_versions_json text NULL,
                created_at timestamptz NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_retrieval_logs_user_id_created_at
                ON retrieval_logs (user_id, created_at);
            """,
            cancellationToken);
    }
}
