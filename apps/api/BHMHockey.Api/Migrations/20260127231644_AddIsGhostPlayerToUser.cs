using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsGhostPlayerToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGhostPlayer",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGhostPlayer",
                table: "Users");
        }
    }
}
