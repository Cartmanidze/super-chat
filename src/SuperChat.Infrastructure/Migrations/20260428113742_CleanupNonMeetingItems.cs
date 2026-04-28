using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanupNonMeetingItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Удаляем legacy-записи Task / Commitment / WaitingOn — после рефакторинга
            // ExtractedItemKind содержит только Meeting, и EnumToStringConverter упадёт
            // при попытке прочитать любой другой kind.
            migrationBuilder.Sql(
                "DELETE FROM extracted_items WHERE kind IN ('Task', 'Commitment', 'WaitingOn');");
            migrationBuilder.Sql(
                "DELETE FROM work_items WHERE kind IN ('Task', 'Commitment', 'WaitingOn');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Откат не имеет смысла: данных не вернуть.
        }
    }
}
