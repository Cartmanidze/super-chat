using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplacesMagicLinksWithVerificationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "magic_links");

            migrationBuilder.CreateTable(
                name: "verification_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    code_salt = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed = table.Column<bool>(type: "boolean", nullable: false),
                    consumed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_codes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_verification_codes_email_created_at",
                table: "verification_codes",
                columns: new[] { "email", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "verification_codes");

            migrationBuilder.CreateTable(
                name: "magic_links",
                columns: table => new
                {
                    value = table.Column<string>(type: "text", nullable: false),
                    consumed = table.Column<bool>(type: "boolean", nullable: false),
                    consumed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_magic_links", x => x.value);
                });

            migrationBuilder.CreateIndex(
                name: "IX_magic_links_email_created_at",
                table: "magic_links",
                columns: new[] { "email", "created_at" });
        }
    }
}
