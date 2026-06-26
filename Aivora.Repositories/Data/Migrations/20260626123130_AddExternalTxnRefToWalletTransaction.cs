using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aivora.Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalTxnRefToWalletTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalTxnRef",
                table: "WalletTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReleasedAt",
                table: "Milestones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ExternalTxnRef",
                table: "WalletTransactions",
                column: "ExternalTxnRef");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_ExternalTxnRef",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalTxnRef",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "Milestones");
        }
    }
}
