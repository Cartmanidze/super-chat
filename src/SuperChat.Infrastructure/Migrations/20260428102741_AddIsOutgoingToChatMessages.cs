using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsOutgoingToChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_outgoing",
                table: "chat_messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_outgoing",
                table: "chat_messages");
        }
    }
}
