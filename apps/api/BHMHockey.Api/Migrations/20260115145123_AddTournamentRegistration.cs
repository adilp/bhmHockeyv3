using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TournamentRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Registered"),
                    Position = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WaitlistPosition = table.Column<int>(type: "integer", nullable: true),
                    PromotedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomResponses = table.Column<string>(type: "jsonb", nullable: true),
                    WaiverStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PaymentStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PaymentMarkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentDeadlineAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentRegistrations_TournamentTeams_AssignedTeamId",
                        column: x => x.AssignedTeamId,
                        principalTable: "TournamentTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TournamentRegistrations_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TournamentRegistrations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRegistrations_AssignedTeamId",
                table: "TournamentRegistrations",
                column: "AssignedTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRegistrations_TournamentId",
                table: "TournamentRegistrations",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRegistrations_TournamentId_Status",
                table: "TournamentRegistrations",
                columns: new[] { "TournamentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRegistrations_TournamentId_UserId",
                table: "TournamentRegistrations",
                columns: new[] { "TournamentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRegistrations_UserId",
                table: "TournamentRegistrations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentRegistrations");
        }
    }
}
