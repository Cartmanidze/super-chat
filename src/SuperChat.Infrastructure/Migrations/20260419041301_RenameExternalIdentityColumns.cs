using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameExternalIdentityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_normalized_messages_user_id_matrix_room_id_matrix_event_id",
                table: "normalized_messages");

            migrationBuilder.RenameColumn(
                name: "matrix_room_id",
                table: "normalized_messages",
                newName: "external_chat_id");

            migrationBuilder.RenameColumn(
                name: "matrix_event_id",
                table: "normalized_messages",
                newName: "external_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_normalized_messages_user_id_external_chat_id_external_messa~",
                table: "normalized_messages",
                columns: new[] { "user_id", "external_chat_id", "external_message_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_normalized_messages_user_id_external_chat_id_external_messa~",
                table: "normalized_messages");

            migrationBuilder.RenameColumn(
                name: "external_chat_id",
                table: "normalized_messages",
                newName: "matrix_room_id");

            migrationBuilder.RenameColumn(
                name: "external_message_id",
                table: "normalized_messages",
                newName: "matrix_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_normalized_messages_user_id_matrix_room_id_matrix_event_id",
                table: "normalized_messages",
                columns: new[] { "user_id", "matrix_room_id", "matrix_event_id" },
                unique: true);
        }
    }
}
