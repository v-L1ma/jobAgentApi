using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace jobAgentApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingCompanyAndLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Levels",
                table: "UserPreferences",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Levels",
                table: "SearchQueries",
                type: "text[]",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""UserPreferences""
                SET ""Levels"" =
                    CASE
                        WHEN COALESCE(TRIM(""Level""), '') = '' THEN ARRAY[]::text[]
                        ELSE ARRAY[""Level""]::text[]
                    END;");

            migrationBuilder.Sql(@"
                UPDATE ""SearchQueries""
                SET ""Levels"" =
                    CASE
                        WHEN COALESCE(TRIM(""Level""), '') = '' THEN ARRAY[]::text[]
                        ELSE ARRAY[""Level""]::text[]
                    END;");

            migrationBuilder.AlterColumn<List<string>>(
                name: "Levels",
                table: "UserPreferences",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0],
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<List<string>>(
                name: "Levels",
                table: "SearchQueries",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0],
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Level",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "SearchQueries");

            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "Jobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "OnboardingCompleted",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "UserPreferences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "SearchQueries",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""UserPreferences""
                SET ""Level"" = COALESCE(""Levels""[1], '');");

            migrationBuilder.Sql(@"
                UPDATE ""SearchQueries""
                SET ""Level"" = COALESCE(""Levels""[1], '');");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "UserPreferences",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "SearchQueries",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Levels",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "Levels",
                table: "SearchQueries");

            migrationBuilder.DropColumn(
                name: "Company",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "OnboardingCompleted",
                table: "AspNetUsers");

        }
    }
}
