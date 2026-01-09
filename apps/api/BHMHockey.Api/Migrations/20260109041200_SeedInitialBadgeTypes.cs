using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialBadgeTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed initial badge types: tournament_winner and beta_tester
            migrationBuilder.Sql(@"
                INSERT INTO ""BadgeTypes"" (""Id"", ""Code"", ""Name"", ""Description"", ""IconName"", ""Category"", ""SortPriority"", ""CreatedAt"")
                VALUES
                    (gen_random_uuid(), 'tournament_winner', 'Tournament Champion', 'Won a BHM Hockey tournament', 'trophy_gold', 'achievement', 1, NOW()),
                    (gen_random_uuid(), 'beta_tester', 'Founding Member', 'Original beta tester', 'star_teal', 'achievement', 2, NOW())
                ON CONFLICT (""Code"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seeded badge types
            migrationBuilder.Sql(@"
                DELETE FROM ""BadgeTypes""
                WHERE ""Code"" IN ('tournament_winner', 'beta_tester');
            ");
        }
    }
}
