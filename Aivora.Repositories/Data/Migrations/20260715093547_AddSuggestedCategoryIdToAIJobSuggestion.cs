using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aivora.Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSuggestedCategoryIdToAIJobSuggestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SuggestedCategoryId",
                table: "AIJobSuggestions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIJobSuggestions_SuggestedCategoryId",
                table: "AIJobSuggestions",
                column: "SuggestedCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_AIJobSuggestions_Categories_SuggestedCategoryId",
                table: "AIJobSuggestions",
                column: "SuggestedCategoryId",
                principalTable: "Categories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIJobSuggestions_Categories_SuggestedCategoryId",
                table: "AIJobSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_AIJobSuggestions_SuggestedCategoryId",
                table: "AIJobSuggestions");

            migrationBuilder.DropColumn(
                name: "SuggestedCategoryId",
                table: "AIJobSuggestions");
        }
    }
}
