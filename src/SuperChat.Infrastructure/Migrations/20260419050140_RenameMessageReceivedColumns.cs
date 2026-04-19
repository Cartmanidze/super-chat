using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameMessageReceivedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ingested_at",
                table: "normalized_messages",
                newName: "received_at");

            migrationBuilder.RenameColumn(
                name: "last_observed_ingested_at",
                table: "chunk_build_checkpoints",
                newName: "last_observed_received_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "received_at",
                table: "normalized_messages",
                newName: "ingested_at");

            migrationBuilder.RenameColumn(
                name: "last_observed_received_at",
                table: "chunk_build_checkpoints",
                newName: "last_observed_ingested_at");
        }
    }
}
