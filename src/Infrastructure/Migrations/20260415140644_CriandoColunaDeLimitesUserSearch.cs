using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace jobAgentApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CriandoColunaDeLimitesUserSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LimitedUntil",
                table: "UserSearchQueries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "SavedJobsCount",
                table: "UserSearchQueries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LimitedUntil",
                table: "UserSearchQueries");

            migrationBuilder.DropColumn(
                name: "SavedJobsCount",
                table: "UserSearchQueries");
        }
    }
}
