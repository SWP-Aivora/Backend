using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aivora.Repositories.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeExternalTxnRefUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_ExternalTxnRef",
                table: "WalletTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ExternalTxnRef",
                table: "WalletTransactions",
                column: "ExternalTxnRef",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_ExternalTxnRef",
                table: "WalletTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ExternalTxnRef",
                table: "WalletTransactions",
                column: "ExternalTxnRef");
        }
    }
}
