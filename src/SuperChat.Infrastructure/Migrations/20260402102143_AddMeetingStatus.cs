using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "meetings",
                type: "text",
                nullable: false,
                defaultValue: "PendingConfirmation");

            migrationBuilder.Sql(
                """
                UPDATE meetings
                SET status = CASE
                    WHEN LOWER(COALESCE(summary, '')) LIKE '%отмена%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%отменили%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%отменяется%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%cancelled%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%canceled%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%cancel%'
                    THEN 'Cancelled'
                    WHEN LOWER(COALESCE(summary, '')) LIKE '%давай%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%лучше%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%не могу%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%не смогу%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%не получится%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%перенес%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%перенос%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%instead%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%resched%'
                    THEN 'Rescheduled'
                    WHEN LOWER(COALESCE(summary, '')) LIKE '%подтверждаю%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%подтверждаем%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%подтверждено%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%confirmed%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%confirm%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%итого%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%финально%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%final%'
                      OR LOWER(COALESCE(summary, '')) LIKE '%договорились%'
                    THEN 'Confirmed'
                    ELSE 'PendingConfirmation'
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_meetings_user_id_status_scheduled_for",
                table: "meetings",
                columns: new[] { "user_id", "status", "scheduled_for" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_meetings_user_id_status_scheduled_for",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "status",
                table: "meetings");
        }
    }
}
