using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiPositionSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add new Positions column (JSONB)
            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "Positions",
                table: "Users",
                type: "jsonb",
                nullable: true);

            // Step 2: Migrate existing data from Position/SkillLevel to Positions JSONB
            // Forward/Defense -> Skater, Goalie -> Goalie
            migrationBuilder.Sql(@"
                UPDATE ""Users""
                SET ""Positions"" = CASE
                    WHEN ""Position"" IN ('Forward', 'Defense') THEN jsonb_build_object('skater', COALESCE(""SkillLevel"", 'Bronze'))
                    WHEN ""Position"" = 'Goalie' THEN jsonb_build_object('goalie', COALESCE(""SkillLevel"", 'Bronze'))
                    ELSE NULL
                END
                WHERE ""Position"" IS NOT NULL;
            ");

            // Step 3: Drop old columns (after data migration)
            migrationBuilder.DropColumn(
                name: "Position",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SkillLevel",
                table: "Users");

            // Step 4: Add RegisteredPosition to EventRegistrations
            migrationBuilder.AddColumn<string>(
                name: "RegisteredPosition",
                table: "EventRegistrations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add back old columns
            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkillLevel",
                table: "Users",
                type: "text",
                nullable: true);

            // Step 2: Migrate data back from Positions JSONB to Position/SkillLevel
            migrationBuilder.Sql(@"
                UPDATE ""Users""
                SET
                    ""Position"" = CASE
                        WHEN ""Positions"" ? 'goalie' THEN 'Goalie'
                        WHEN ""Positions"" ? 'skater' THEN 'Forward'
                        ELSE NULL
                    END,
                    ""SkillLevel"" = COALESCE(
                        ""Positions"" ->> 'goalie',
                        ""Positions"" ->> 'skater'
                    )
                WHERE ""Positions"" IS NOT NULL;
            ");

            // Step 3: Drop new columns
            migrationBuilder.DropColumn(
                name: "Positions",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RegisteredPosition",
                table: "EventRegistrations");
        }
    }
}
