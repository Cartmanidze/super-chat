using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SuperChat.Infrastructure.Shared.Persistence;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    [DbContext(typeof(SuperChatDbContext))]
    [Migration("20260408132351_DeduplicateExistingMeetings")]
    public partial class DeduplicateExistingMeetings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- Statement 1: перенести resolution-trace от лучшего donor
                WITH ranked AS (
                    SELECT id, user_id, source_room,
                           date_trunc('minute', scheduled_for) AS sf_minute,
                           lower(trim(summary)) AS norm_summary,
                           resolved_at, resolution_kind, resolution_source,
                           resolution_confidence, resolution_model, resolution_evidence_json,
                           ROW_NUMBER() OVER (
                               PARTITION BY user_id, source_room,
                                            date_trunc('minute', scheduled_for),
                                            lower(trim(summary))
                               ORDER BY
                                   CASE WHEN source_event_id NOT LIKE 'chunk:%' THEN 0 ELSE 1 END,
                                   created_at
                           ) AS rn
                    FROM meetings
                ),
                resolution_donor AS (
                    SELECT DISTINCT ON (r_keep.id)
                           r_keep.id AS keeper_id,
                           r_donor.resolved_at,
                           r_donor.resolution_kind,
                           r_donor.resolution_source,
                           r_donor.resolution_confidence,
                           r_donor.resolution_model,
                           r_donor.resolution_evidence_json
                    FROM ranked r_keep
                    JOIN ranked r_donor
                      ON r_keep.user_id = r_donor.user_id
                     AND r_keep.source_room = r_donor.source_room
                     AND r_keep.sf_minute = r_donor.sf_minute
                     AND r_keep.norm_summary = r_donor.norm_summary
                     AND r_keep.rn = 1
                     AND r_donor.rn > 1
                    WHERE r_keep.resolved_at IS NULL
                      AND r_donor.resolved_at IS NOT NULL
                    ORDER BY r_keep.id,
                             r_donor.resolution_confidence DESC NULLS LAST
                )
                UPDATE meetings m
                SET resolved_at = rd.resolved_at,
                    resolution_kind = rd.resolution_kind,
                    resolution_source = rd.resolution_source,
                    resolution_confidence = rd.resolution_confidence,
                    resolution_model = rd.resolution_model,
                    resolution_evidence_json = rd.resolution_evidence_json,
                    updated_at = now()
                FROM resolution_donor rd
                WHERE m.id = rd.keeper_id;

                -- Statement 2: перенести status от лучшего donor независимо от resolution
                WITH ranked AS (
                    SELECT id, user_id, source_room, status,
                           date_trunc('minute', scheduled_for) AS sf_minute,
                           lower(trim(summary)) AS norm_summary,
                           ROW_NUMBER() OVER (
                               PARTITION BY user_id, source_room,
                                            date_trunc('minute', scheduled_for),
                                            lower(trim(summary))
                               ORDER BY
                                   CASE WHEN source_event_id NOT LIKE 'chunk:%' THEN 0 ELSE 1 END,
                                   created_at
                           ) AS rn
                    FROM meetings
                ),
                status_donor AS (
                    SELECT DISTINCT ON (r_keep.id)
                           r_keep.id AS keeper_id,
                           r_donor.status AS donor_status
                    FROM ranked r_keep
                    JOIN ranked r_donor
                      ON r_keep.user_id = r_donor.user_id
                     AND r_keep.source_room = r_donor.source_room
                     AND r_keep.sf_minute = r_donor.sf_minute
                     AND r_keep.norm_summary = r_donor.norm_summary
                     AND r_keep.rn = 1
                     AND r_donor.rn > 1
                    WHERE r_keep.status = 'PendingConfirmation'
                      AND r_donor.status != 'PendingConfirmation'
                    ORDER BY r_keep.id,
                             CASE r_donor.status
                                 WHEN 'Confirmed' THEN 0
                                 WHEN 'Rescheduled' THEN 1
                                 WHEN 'Cancelled' THEN 2
                                 ELSE 3
                             END
                )
                UPDATE meetings m
                SET status = sd.donor_status,
                    updated_at = now()
                FROM status_donor sd
                WHERE m.id = sd.keeper_id;

                -- Statement 3: удалить дубликаты
                WITH ranked AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY user_id, source_room,
                                            date_trunc('minute', scheduled_for),
                                            lower(trim(summary))
                               ORDER BY
                                   CASE WHEN source_event_id NOT LIKE 'chunk:%' THEN 0 ELSE 1 END,
                                   created_at
                           ) AS rn
                    FROM meetings
                )
                DELETE FROM meetings
                WHERE id IN (SELECT id FROM ranked WHERE rn > 1);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
