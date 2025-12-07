using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiSkillLevelSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkillLevel",
                table: "Organizations");

            migrationBuilder.AddColumn<List<string>>(
                name: "SkillLevels",
                table: "Organizations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "SkillLevels",
                table: "Events",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkillLevels",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SkillLevels",
                table: "Events");

            migrationBuilder.AddColumn<string>(
                name: "SkillLevel",
                table: "Organizations",
                type: "text",
                nullable: true);
        }
    }
}
