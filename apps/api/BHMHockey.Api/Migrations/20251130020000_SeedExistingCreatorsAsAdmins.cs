using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedExistingCreatorsAsAdmins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert existing organization creators as admins
            // This ensures backward compatibility with the multi-admin refactor
            migrationBuilder.Sql(@"
                INSERT INTO ""OrganizationAdmins"" (""Id"", ""OrganizationId"", ""UserId"", ""AddedAt"", ""AddedByUserId"")
                SELECT
                    gen_random_uuid(),
                    o.""Id"",
                    o.""CreatorId"",
                    NOW(),
                    NULL
                FROM ""Organizations"" o
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""OrganizationAdmins"" oa
                    WHERE oa.""OrganizationId"" = o.""Id""
                    AND oa.""UserId"" = o.""CreatorId""
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove auto-seeded admins (those where AddedByUserId is NULL)
            // This only removes the initial seeded admins, not manually added ones
            migrationBuilder.Sql(@"
                DELETE FROM ""OrganizationAdmins""
                WHERE ""AddedByUserId"" IS NULL;
            ");
        }
    }
}
