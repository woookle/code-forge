using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForgeAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddMicroservicesSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchitectureType",
                table: "Projects",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ServiceName",
                table: "Entities",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchitectureType",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ServiceName",
                table: "Entities");
        }
    }
}
