using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatTitleToNormalizedMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chat_title",
                table: "normalized_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chat_title",
                table: "normalized_messages");
        }
    }
}
