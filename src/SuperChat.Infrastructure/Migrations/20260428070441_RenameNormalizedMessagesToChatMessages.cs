using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameNormalizedMessagesToChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The table is renamed in place to preserve existing rows. The
            // primary key, indexes and FK column names that referenced the
            // old "normalized_messages" naming are renamed alongside.
            migrationBuilder.RenameTable(
                name: "normalized_messages",
                newName: "chat_messages");

            migrationBuilder.RenameIndex(
                table: "chat_messages",
                name: "IX_normalized_messages_processed",
                newName: "IX_chat_messages_processed");

            migrationBuilder.RenameIndex(
                table: "chat_messages",
                name: "IX_normalized_messages_user_id_external_chat_id_external_messa~",
                newName: "IX_chat_messages_user_id_external_chat_id_external_message_id");

            migrationBuilder.Sql("ALTER TABLE chat_messages RENAME CONSTRAINT \"PK_normalized_messages\" TO \"PK_chat_messages\";");

            migrationBuilder.RenameColumn(
                table: "message_chunks",
                name: "first_normalized_message_id",
                newName: "first_chat_message_id");

            migrationBuilder.RenameColumn(
                table: "message_chunks",
                name: "last_normalized_message_id",
                newName: "last_chat_message_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                table: "message_chunks",
                name: "last_chat_message_id",
                newName: "last_normalized_message_id");

            migrationBuilder.RenameColumn(
                table: "message_chunks",
                name: "first_chat_message_id",
                newName: "first_normalized_message_id");

            migrationBuilder.Sql("ALTER TABLE chat_messages RENAME CONSTRAINT \"PK_chat_messages\" TO \"PK_normalized_messages\";");

            migrationBuilder.RenameIndex(
                table: "chat_messages",
                name: "IX_chat_messages_user_id_external_chat_id_external_message_id",
                newName: "IX_normalized_messages_user_id_external_chat_id_external_messa~");

            migrationBuilder.RenameIndex(
                table: "chat_messages",
                name: "IX_chat_messages_processed",
                newName: "IX_normalized_messages_processed");

            migrationBuilder.RenameTable(
                name: "chat_messages",
                newName: "normalized_messages");
        }
    }
}
