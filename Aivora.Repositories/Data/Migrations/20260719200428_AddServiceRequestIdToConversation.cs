using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aivora.Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestIdToConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServiceRequestId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ServiceRequestId",
                table: "Conversations",
                column: "ServiceRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_ServiceRequests_ServiceRequestId",
                table: "Conversations",
                column: "ServiceRequestId",
                principalTable: "ServiceRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_ServiceRequests_ServiceRequestId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ServiceRequestId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ServiceRequestId",
                table: "Conversations");
        }
    }
}
