using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationWaivers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationWaivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationWaivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationWaivers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationWaivers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WaiverAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaiverId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaiverAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaiverAcceptances_OrganizationWaivers_WaiverId",
                        column: x => x.WaiverId,
                        principalTable: "OrganizationWaivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WaiverAcceptances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationWaivers_CreatedByUserId",
                table: "OrganizationWaivers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationWaivers_OrganizationId_Version",
                table: "OrganizationWaivers",
                columns: new[] { "OrganizationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaiverAcceptances_UserId_WaiverId",
                table: "WaiverAcceptances",
                columns: new[] { "UserId", "WaiverId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaiverAcceptances_WaiverId",
                table: "WaiverAcceptances",
                column: "WaiverId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaiverAcceptances");

            migrationBuilder.DropTable(
                name: "OrganizationWaivers");
        }
    }
}
