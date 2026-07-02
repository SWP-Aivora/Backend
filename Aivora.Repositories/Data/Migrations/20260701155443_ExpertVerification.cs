using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aivora.Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpertVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "ExpertProfiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "ExpertProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedByAdminId",
                table: "ExpertProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpertVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    ProfileScore = table.Column<int>(type: "integer", nullable: false),
                    SkillsScore = table.Column<int>(type: "integer", nullable: false),
                    CertificatesScore = table.Column<int>(type: "integer", nullable: false),
                    ProfileWeight = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SkillsWeight = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CertificatesWeight = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsPassed = table.Column<bool>(type: "boolean", nullable: false),
                    Feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    AiProcessingId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LastProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsProfileProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    IsSkillsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    IsCertificatesProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    AppealAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppealReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AppealRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppealAdminFeedback = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CurrentStage = table.Column<int>(type: "integer", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpertVerifications_ExpertProfiles_ExpertId",
                        column: x => x.ExpertId,
                        principalTable: "ExpertProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VerificationCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IssuingOrganization = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CertificateUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerificationNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExpertVerificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationCertificates_ExpertProfiles_ExpertId",
                        column: x => x.ExpertId,
                        principalTable: "ExpertProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VerificationCertificates_ExpertVerifications_ExpertVerifica~",
                        column: x => x.ExpertVerificationId,
                        principalTable: "ExpertVerifications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertVerifications_AiProcessingId",
                table: "ExpertVerifications",
                column: "AiProcessingId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertVerifications_ExpertId",
                table: "ExpertVerifications",
                column: "ExpertId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpertVerifications_LastProcessedAt",
                table: "ExpertVerifications",
                column: "LastProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExpertVerifications_Status",
                table: "ExpertVerifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCertificates_ExpertId",
                table: "VerificationCertificates",
                column: "ExpertId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCertificates_ExpertVerificationId",
                table: "VerificationCertificates",
                column: "ExpertVerificationId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCertificates_ExpiryDate",
                table: "VerificationCertificates",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCertificates_IssuingOrganization",
                table: "VerificationCertificates",
                column: "IssuingOrganization");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationCertificates_IsVerified",
                table: "VerificationCertificates",
                column: "IsVerified");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerificationCertificates");

            migrationBuilder.DropTable(
                name: "ExpertVerifications");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "ExpertProfiles");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "ExpertProfiles");

            migrationBuilder.DropColumn(
                name: "VerifiedByAdminId",
                table: "ExpertProfiles");
        }
    }
}
