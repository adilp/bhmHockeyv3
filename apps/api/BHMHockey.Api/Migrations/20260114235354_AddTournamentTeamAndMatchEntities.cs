using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentTeamAndMatchEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TournamentTeams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CaptainUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Registered"),
                    WaitlistPosition = table.Column<int>(type: "integer", nullable: true),
                    Seed = table.Column<int>(type: "integer", nullable: true),
                    FinalPlacement = table.Column<int>(type: "integer", nullable: true),
                    HasBye = table.Column<bool>(type: "boolean", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Losses = table.Column<int>(type: "integer", nullable: false),
                    Ties = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    GoalsFor = table.Column<int>(type: "integer", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "integer", nullable: false),
                    PaymentStatus = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Users_CaptainUserId",
                        column: x => x.CaptainUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TournamentMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    AwayTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    MatchNumber = table.Column<int>(type: "integer", nullable: false),
                    BracketPosition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsBye = table.Column<bool>(type: "boolean", nullable: false),
                    ScheduledTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Venue = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Scheduled"),
                    HomeScore = table.Column<int>(type: "integer", nullable: true),
                    AwayScore = table.Column<int>(type: "integer", nullable: true),
                    WinnerTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    ForfeitReason = table.Column<string>(type: "text", nullable: true),
                    NextMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoserNextMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentMatches_LoserNextMatchId",
                        column: x => x.LoserNextMatchId,
                        principalTable: "TournamentMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentMatches_NextMatchId",
                        column: x => x.NextMatchId,
                        principalTable: "TournamentMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentTeams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "TournamentTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentTeams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "TournamentTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_TournamentTeams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "TournamentTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TournamentMatches_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_AwayTeamId",
                table: "TournamentMatches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_HomeTeamId",
                table: "TournamentMatches",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_LoserNextMatchId",
                table: "TournamentMatches",
                column: "LoserNextMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_NextMatchId",
                table: "TournamentMatches",
                column: "NextMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_TournamentId",
                table: "TournamentMatches",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_TournamentId_Round_MatchNumber",
                table: "TournamentMatches",
                columns: new[] { "TournamentId", "Round", "MatchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatches_WinnerTeamId",
                table: "TournamentMatches",
                column: "WinnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_CaptainUserId",
                table: "TournamentTeams",
                column: "CaptainUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_TournamentId",
                table: "TournamentTeams",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_TournamentId_Status",
                table: "TournamentTeams",
                columns: new[] { "TournamentId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentMatches");

            migrationBuilder.DropTable(
                name: "TournamentTeams");
        }
    }
}
