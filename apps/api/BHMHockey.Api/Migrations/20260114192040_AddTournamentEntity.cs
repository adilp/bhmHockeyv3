using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "SingleElimination"),
                    TeamFormation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "OrganizerAssigned"),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistrationDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PostponedToDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxTeams = table.Column<int>(type: "integer", nullable: false),
                    MinPlayersPerTeam = table.Column<int>(type: "integer", nullable: true),
                    MaxPlayersPerTeam = table.Column<int>(type: "integer", nullable: true),
                    AllowMultiTeam = table.Column<bool>(type: "boolean", nullable: false),
                    AllowSubstitutions = table.Column<bool>(type: "boolean", nullable: false),
                    EntryFee = table.Column<decimal>(type: "numeric", nullable: false),
                    FeeType = table.Column<string>(type: "text", nullable: true),
                    PointsWin = table.Column<int>(type: "integer", nullable: false),
                    PointsTie = table.Column<int>(type: "integer", nullable: false),
                    PointsLoss = table.Column<int>(type: "integer", nullable: false),
                    PlayoffFormat = table.Column<string>(type: "text", nullable: true),
                    PlayoffTeamsCount = table.Column<int>(type: "integer", nullable: true),
                    RulesContent = table.Column<string>(type: "text", nullable: true),
                    WaiverUrl = table.Column<string>(type: "text", nullable: true),
                    Venue = table.Column<string>(type: "text", nullable: true),
                    NotificationSettings = table.Column<string>(type: "jsonb", nullable: true),
                    CustomQuestions = table.Column<string>(type: "jsonb", nullable: true),
                    EligibilityRequirements = table.Column<string>(type: "jsonb", nullable: true),
                    TiebreakerOrder = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tournaments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tournaments_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TournamentAdmins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Admin"),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentAdmins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentAdmins_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TournamentAdmins_Users_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TournamentAdmins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentAdmins_AddedByUserId",
                table: "TournamentAdmins",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentAdmins_TournamentId_UserId",
                table: "TournamentAdmins",
                columns: new[] { "TournamentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentAdmins_UserId",
                table: "TournamentAdmins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatorId",
                table: "Tournaments",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_OrganizationId",
                table: "Tournaments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_Status",
                table: "Tournaments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentAdmins");

            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
