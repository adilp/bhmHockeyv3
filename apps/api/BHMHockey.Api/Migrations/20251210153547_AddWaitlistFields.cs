using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDeadlineAt",
                table: "EventRegistrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PromotedAt",
                table: "EventRegistrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WaitlistPosition",
                table: "EventRegistrations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentDeadlineAt",
                table: "EventRegistrations");

            migrationBuilder.DropColumn(
                name: "PromotedAt",
                table: "EventRegistrations");

            migrationBuilder.DropColumn(
                name: "WaitlistPosition",
                table: "EventRegistrations");
        }
    }
}
