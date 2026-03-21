using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResolutionTraceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "resolution_confidence",
                table: "work_items",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolution_evidence_json",
                table: "work_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolution_model",
                table: "work_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "resolution_confidence",
                table: "meetings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolution_evidence_json",
                table: "meetings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolution_model",
                table: "meetings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "resolution_confidence",
                table: "extracted_items",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolution_evidence_json",
                table: "extracted_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolution_model",
                table: "extracted_items",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "resolution_confidence",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "resolution_evidence_json",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "resolution_model",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "resolution_confidence",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "resolution_evidence_json",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "resolution_model",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "resolution_confidence",
                table: "extracted_items");

            migrationBuilder.DropColumn(
                name: "resolution_evidence_json",
                table: "extracted_items");

            migrationBuilder.DropColumn(
                name: "resolution_model",
                table: "extracted_items");
        }
    }
}
