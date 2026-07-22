using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aivora.Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiRankToRecommendationResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiRank",
                table: "RecommendationResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiRank",
                table: "RecommendationResults");
        }
    }
}
