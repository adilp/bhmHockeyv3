using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationEventDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DefaultCost",
                table: "Organizations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultDayOfWeek",
                table: "Organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultDurationMinutes",
                table: "Organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultMaxPlayers",
                table: "Organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "DefaultStartTime",
                table: "Organizations",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultVenue",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultVisibility",
                table: "Organizations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultCost",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DefaultDayOfWeek",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DefaultDurationMinutes",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DefaultMaxPlayers",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DefaultStartTime",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DefaultVenue",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DefaultVisibility",
                table: "Organizations");
        }
    }
}
