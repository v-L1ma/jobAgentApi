using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace jobAgentApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionandoColunaPlataformaParaVagas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Platform",
                table: "Jobs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Platform",
                table: "Jobs");
        }
    }
}
