using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCelebratedAtToUserBadge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CelebratedAt",
                table: "UserBadges",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CelebratedAt",
                table: "UserBadges");
        }
    }
}
