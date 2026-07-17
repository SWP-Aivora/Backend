using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aivora.Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "ExpertSkills",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ExpertVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceFileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EvidencePublicId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AIConfidenceScore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AIReasoning = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminDecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertVerifications_ExpertSkills_ExpertSkillId",
                        column: x => x.ExpertSkillId,
                        principalTable: "ExpertSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpertVerifications_Users_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertVerifications_AdminId",
                table: "ExpertVerifications",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertVerifications_ExpertSkillId_CreatedAt",
                table: "ExpertVerifications",
                columns: new[] { "ExpertSkillId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpertVerifications");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "ExpertSkills");
        }
    }
}
