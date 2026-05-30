using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aivora.Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProfileRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RatingAvg",
                table: "ExpertProfiles",
                newName: "Rating");

            migrationBuilder.Sql("ALTER TABLE \"JobPosts\" ALTER COLUMN \"ExperienceLevel\" TYPE integer USING \"ExperienceLevel\"::integer");

            migrationBuilder.AddColumn<int>(
                name: "TotalReviews",
                table: "ExpertProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Rating",
                table: "ClientProfiles",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalReviews",
                table: "ClientProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalReviews",
                table: "ExpertProfiles");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "TotalReviews",
                table: "ClientProfiles");

            migrationBuilder.RenameColumn(
                name: "Rating",
                table: "ExpertProfiles",
                newName: "RatingAvg");

            migrationBuilder.AlterColumn<string>(
                name: "ExperienceLevel",
                table: "JobPosts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
