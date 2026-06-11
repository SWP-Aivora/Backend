using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aivora.Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDueDaysAndAcceptanceCriteriaToJobPostMilestones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "JobPostMilestones",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DueDays",
                table: "JobPostMilestones",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "JobPostMilestones");

            migrationBuilder.DropColumn(
                name: "DueDays",
                table: "JobPostMilestones");
        }
    }
}
