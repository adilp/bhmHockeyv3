using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BHMHockey.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWaiverSignatureFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GuardianDate",
                table: "WaiverAcceptances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianName",
                table: "WaiverAcceptances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianSignature",
                table: "WaiverAcceptances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MinorDateOfBirth",
                table: "WaiverAcceptances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinorParticipantName",
                table: "WaiverAcceptances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ParticipantDate",
                table: "WaiverAcceptances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParticipantName",
                table: "WaiverAcceptances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuardianDate",
                table: "WaiverAcceptances");

            migrationBuilder.DropColumn(
                name: "GuardianName",
                table: "WaiverAcceptances");

            migrationBuilder.DropColumn(
                name: "GuardianSignature",
                table: "WaiverAcceptances");

            migrationBuilder.DropColumn(
                name: "MinorDateOfBirth",
                table: "WaiverAcceptances");

            migrationBuilder.DropColumn(
                name: "MinorParticipantName",
                table: "WaiverAcceptances");

            migrationBuilder.DropColumn(
                name: "ParticipantDate",
                table: "WaiverAcceptances");

            migrationBuilder.DropColumn(
                name: "ParticipantName",
                table: "WaiverAcceptances");
        }
    }
}
