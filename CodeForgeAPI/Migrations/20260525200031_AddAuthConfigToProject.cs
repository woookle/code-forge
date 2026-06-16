using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForgeAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthConfigToProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthConfig",
                table: "Projects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthConfig",
                table: "Projects");
        }
    }
}
