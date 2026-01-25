using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventIsRosterPublished : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add column with default true so existing events are published
            migrationBuilder.AddColumn<bool>(
                name: "IsRosterPublished",
                table: "Events",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Change default for new rows to false (new events start unpublished)
            migrationBuilder.Sql("ALTER TABLE \"Events\" ALTER COLUMN \"IsRosterPublished\" SET DEFAULT false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRosterPublished",
                table: "Events");
        }
    }
}
