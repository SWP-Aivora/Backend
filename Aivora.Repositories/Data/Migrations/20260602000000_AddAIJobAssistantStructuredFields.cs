using Aivora.Repositories.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aivora.Repositories.Data.Migrations;

[Migration("20260602000000_AddAIJobAssistantStructuredFields")]
[DbContext(typeof(AivoraDbContext))]
public partial class AddAIJobAssistantStructuredFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SuggestedBudgetType",
            table: "AIJobSuggestions",
            type: "text",
            nullable: false,
            defaultValue: "FIXED");

        migrationBuilder.AddColumn<string>(
            name: "Currency",
            table: "AIJobSuggestions",
            type: "character varying(10)",
            maxLength: 10,
            nullable: false,
            defaultValue: "AICOIN");

        migrationBuilder.AddColumn<string>(
            name: "SuggestedExperienceLevel",
            table: "AIJobSuggestions",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SuggestedBusinessDomain",
            table: "AIJobSuggestions",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SuggestedExpectedOutcome",
            table: "AIJobSuggestions",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ClarifyingAnswersJson",
            table: "AIJobSuggestions",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RejectionReason",
            table: "AIJobSuggestions",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SuggestedBudgetType",
            table: "AIJobSuggestions");

        migrationBuilder.DropColumn(
            name: "Currency",
            table: "AIJobSuggestions");

        migrationBuilder.DropColumn(
            name: "SuggestedExperienceLevel",
            table: "AIJobSuggestions");

        migrationBuilder.DropColumn(
            name: "SuggestedBusinessDomain",
            table: "AIJobSuggestions");

        migrationBuilder.DropColumn(
            name: "SuggestedExpectedOutcome",
            table: "AIJobSuggestions");

        migrationBuilder.DropColumn(
            name: "ClarifyingAnswersJson",
            table: "AIJobSuggestions");

        migrationBuilder.DropColumn(
            name: "RejectionReason",
            table: "AIJobSuggestions");
    }
}
