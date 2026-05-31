using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aivora.Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class FullSchema_20260531 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Payments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Payments");
        }
    }
}
