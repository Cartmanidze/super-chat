using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameSourceRoomToExternalChatId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "source_room",
                table: "work_items",
                newName: "external_chat_id");

            migrationBuilder.RenameColumn(
                name: "source_room",
                table: "meetings",
                newName: "external_chat_id");

            migrationBuilder.RenameColumn(
                name: "source_room",
                table: "extracted_items",
                newName: "external_chat_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "external_chat_id",
                table: "work_items",
                newName: "source_room");

            migrationBuilder.RenameColumn(
                name: "external_chat_id",
                table: "meetings",
                newName: "source_room");

            migrationBuilder.RenameColumn(
                name: "external_chat_id",
                table: "extracted_items",
                newName: "source_room");
        }
    }
}
