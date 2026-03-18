using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_sessions",
                columns: table => new
                {
                    token = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_sessions", x => x.token);
                });

            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chunk_build_checkpoints",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_observed_ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_observed_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunk_build_checkpoints", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "extracted_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    source_room = table.Column<string>(type: "text", nullable: false),
                    source_event_id = table.Column<string>(type: "text", nullable: false),
                    person = table.Column<string>(type: "text", nullable: true),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_kind = table.Column<string>(type: "text", nullable: true),
                    resolution_source = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extracted_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feedback_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feedback_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "magic_links",
                columns: table => new
                {
                    value = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed = table.Column<bool>(type: "boolean", nullable: false),
                    consumed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_magic_links", x => x.value);
                });

            migrationBuilder.CreateTable(
                name: "matrix_identities",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matrix_user_id = table.Column<string>(type: "text", nullable: false),
                    access_token = table.Column<string>(type: "text", nullable: false),
                    provisioned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matrix_identities", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "meeting_projection_checkpoints",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_observed_chunk_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_observed_chunk_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_projection_checkpoints", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "meetings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    source_room = table.Column<string>(type: "text", nullable: false),
                    source_event_id = table.Column<string>(type: "text", nullable: false),
                    person = table.Column<string>(type: "text", nullable: true),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scheduled_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    meeting_provider = table.Column<string>(type: "text", nullable: true),
                    meeting_join_url = table.Column<string>(type: "text", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_kind = table.Column<string>(type: "text", nullable: true),
                    resolution_source = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meetings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    transport = table.Column<string>(type: "text", nullable: false),
                    chat_id = table.Column<string>(type: "text", nullable: false),
                    peer_id = table.Column<string>(type: "text", nullable: true),
                    thread_id = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    message_count = table.Column<int>(type: "integer", nullable: false),
                    first_normalized_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_normalized_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ts_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ts_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    content_hash = table.Column<string>(type: "text", nullable: false),
                    chunk_version = table.Column<int>(type: "integer", nullable: false),
                    embedding_version = table.Column<string>(type: "text", nullable: true),
                    qdrant_point_id = table.Column<string>(type: "text", nullable: true),
                    indexed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_chunks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "normalized_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    matrix_room_id = table.Column<string>(type: "text", nullable: false),
                    matrix_event_id = table.Column<string>(type: "text", nullable: false),
                    sender_name = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_normalized_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pilot_invites",
                columns: table => new
                {
                    email = table.Column<string>(type: "text", nullable: false),
                    invited_by = table.Column<string>(type: "text", nullable: false),
                    invited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pilot_invites", x => x.email);
                });

            migrationBuilder.CreateTable(
                name: "retrieval_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    query_text = table.Column<string>(type: "text", nullable: false),
                    query_kind = table.Column<string>(type: "text", nullable: false),
                    filters_json = table.Column<string>(type: "text", nullable: true),
                    candidate_count = table.Column<int>(type: "integer", nullable: false),
                    selected_chunk_ids_json = table.Column<string>(type: "text", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    model_versions_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retrieval_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_checkpoints",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    next_batch_token = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_checkpoints", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "telegram_connections",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    web_login_url = table.Column<string>(type: "text", nullable: true),
                    management_room_id = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    development_seeded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_connections", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "work_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    source_room = table.Column<string>(type: "text", nullable: false),
                    source_event_id = table.Column<string>(type: "text", nullable: false),
                    person = table.Column<string>(type: "text", nullable: true),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_kind = table.Column<string>(type: "text", nullable: true),
                    resolution_source = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_sessions_user_id",
                table: "api_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_app_users_email",
                table: "app_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_extracted_items_user_id_observed_at",
                table: "extracted_items",
                columns: new[] { "user_id", "observed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_feedback_events_user_id_created_at",
                table: "feedback_events",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_magic_links_email_created_at",
                table: "magic_links",
                columns: new[] { "email", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_matrix_identities_matrix_user_id",
                table: "matrix_identities",
                column: "matrix_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meetings_user_id_scheduled_for",
                table: "meetings",
                columns: new[] { "user_id", "scheduled_for" });

            migrationBuilder.CreateIndex(
                name: "IX_meetings_user_id_source_event_id",
                table: "meetings",
                columns: new[] { "user_id", "source_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_chunks_user_id_chat_id_ts_from",
                table: "message_chunks",
                columns: new[] { "user_id", "chat_id", "ts_from" });

            migrationBuilder.CreateIndex(
                name: "IX_message_chunks_user_id_ts_from",
                table: "message_chunks",
                columns: new[] { "user_id", "ts_from" });

            migrationBuilder.CreateIndex(
                name: "IX_normalized_messages_processed",
                table: "normalized_messages",
                column: "processed");

            migrationBuilder.CreateIndex(
                name: "IX_normalized_messages_user_id_matrix_room_id_matrix_event_id",
                table: "normalized_messages",
                columns: new[] { "user_id", "matrix_room_id", "matrix_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_logs_user_id_created_at",
                table: "retrieval_logs",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_connections_state",
                table: "telegram_connections",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_user_id_due_at",
                table: "work_items",
                columns: new[] { "user_id", "due_at" });

            migrationBuilder.CreateIndex(
                name: "IX_work_items_user_id_observed_at",
                table: "work_items",
                columns: new[] { "user_id", "observed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_sessions");

            migrationBuilder.DropTable(
                name: "app_users");

            migrationBuilder.DropTable(
                name: "chunk_build_checkpoints");

            migrationBuilder.DropTable(
                name: "extracted_items");

            migrationBuilder.DropTable(
                name: "feedback_events");

            migrationBuilder.DropTable(
                name: "magic_links");

            migrationBuilder.DropTable(
                name: "matrix_identities");

            migrationBuilder.DropTable(
                name: "meeting_projection_checkpoints");

            migrationBuilder.DropTable(
                name: "meetings");

            migrationBuilder.DropTable(
                name: "message_chunks");

            migrationBuilder.DropTable(
                name: "normalized_messages");

            migrationBuilder.DropTable(
                name: "pilot_invites");

            migrationBuilder.DropTable(
                name: "retrieval_logs");

            migrationBuilder.DropTable(
                name: "sync_checkpoints");

            migrationBuilder.DropTable(
                name: "telegram_connections");

            migrationBuilder.DropTable(
                name: "work_items");
        }
    }
}
