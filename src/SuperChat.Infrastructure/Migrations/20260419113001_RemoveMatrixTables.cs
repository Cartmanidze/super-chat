using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMatrixTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "matrix_identities");

            migrationBuilder.DropTable(
                name: "sync_checkpoints");

            migrationBuilder.DropColumn(
                name: "management_room_id",
                table: "telegram_connections");

            migrationBuilder.DropColumn(
                name: "web_login_url",
                table: "telegram_connections");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "management_room_id",
                table: "telegram_connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "web_login_url",
                table: "telegram_connections",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "matrix_identities",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_token = table.Column<string>(type: "text", nullable: false),
                    matrix_user_id = table.Column<string>(type: "text", nullable: false),
                    provisioned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matrix_identities", x => x.user_id);
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

            migrationBuilder.CreateIndex(
                name: "IX_matrix_identities_matrix_user_id",
                table: "matrix_identities",
                column: "matrix_user_id",
                unique: true);
        }
    }
}
