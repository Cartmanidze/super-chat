using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SuperChat.Infrastructure.Persistence;

public static class LegacyDatabaseMigrationBootstrapper
{
    public static async Task PrepareAsync(
        SuperChatDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var migrations = dbContext.Database.GetMigrations().ToList();
        if (migrations.Count == 0)
        {
            return;
        }

        if (await MigrationHistoryExistsAsync(dbContext, cancellationToken))
        {
            return;
        }

        if (!await LegacySchemaExistsAsync(dbContext, cancellationToken))
        {
            return;
        }

        logger.LogWarning("Legacy application schema detected without EF migration history. Applying compatibility upgrade and baselining migrations.");

        if (dbContext.Database.IsNpgsql())
        {
            await UpgradeLegacyPostgresSchemaAsync(dbContext, cancellationToken);
        }

        await EnsureMigrationHistoryTableAsync(dbContext, cancellationToken);
        await BaselineAllMigrationsAsync(dbContext, migrations, cancellationToken);

        logger.LogInformation("Legacy schema was aligned and {MigrationCount} migrations were marked as applied.", migrations.Count);
    }

    private static async Task<bool> MigrationHistoryExistsAsync(SuperChatDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = dbContext.Database.IsSqlite()
                ? "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory';"
                : """
                  SELECT COUNT(*)
                  FROM information_schema.tables
                  WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory';
                  """;

            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            return count > 0;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<bool> LegacySchemaExistsAsync(SuperChatDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = dbContext.Database.IsSqlite()
                ? "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'pilot_invites';"
                : """
                  SELECT COUNT(*)
                  FROM information_schema.tables
                  WHERE table_schema = 'public' AND table_name = 'pilot_invites';
                  """;

            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            return count > 0;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static Task EnsureMigrationHistoryTableAsync(SuperChatDbContext dbContext, CancellationToken cancellationToken)
    {
        return dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """,
            cancellationToken);
    }

    private static async Task BaselineAllMigrationsAsync(
        SuperChatDbContext dbContext,
        IReadOnlyList<string> migrations,
        CancellationToken cancellationToken)
    {
        var productVersion = ResolveProductVersion();

        foreach (var migrationId in migrations)
        {
            if (dbContext.Database.IsSqlite())
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ({0}, {1});
                    """,
                    [migrationId, productVersion],
                    cancellationToken);

                continue;
            }

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ({0}, {1})
                ON CONFLICT ("MigrationId") DO NOTHING;
                """,
                [migrationId, productVersion],
                cancellationToken);
        }
    }

    private static string ResolveProductVersion()
    {
        var informationalVersion = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return string.IsNullOrWhiteSpace(informationalVersion)
            ? "10.0.0"
            : informationalVersion.Split('+', 2, StringSplitOptions.TrimEntries)[0];
    }

    private static Task UpgradeLegacyPostgresSchemaAsync(SuperChatDbContext dbContext, CancellationToken cancellationToken)
    {
        return dbContext.Database.ExecuteSqlRawAsync(
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
