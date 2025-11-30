using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class AgreementUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agreements_users_buyer_id",
                table: "agreements");

            migrationBuilder.DropForeignKey(
                name: "FK_agreements_users_seller_id",
                table: "agreements");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_users_payee_id",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_users_payer_id",
                table: "payments");

            migrationBuilder.RenameColumn(
                name: "payer_id",
                table: "payments",
                newName: "tip_to_user_id");

            migrationBuilder.RenameColumn(
                name: "payee_id",
                table: "payments",
                newName: "tip_from_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_payments_payer_id",
                table: "payments",
                newName: "IX_payments_tip_to_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_payments_payee_id",
                table: "payments",
                newName: "IX_payments_tip_from_user_id");

            migrationBuilder.RenameColumn(
                name: "seller_id",
                table: "agreements",
                newName: "requester_id");

            migrationBuilder.RenameColumn(
                name: "buyer_id",
                table: "agreements",
                newName: "provider_id");

            migrationBuilder.RenameIndex(
                name: "IX_agreements_seller_id",
                table: "agreements",
                newName: "IX_agreements_requester_id");

            migrationBuilder.RenameIndex(
                name: "IX_agreements_buyer_id",
                table: "agreements",
                newName: "IX_agreements_provider_id");

            migrationBuilder.AddForeignKey(
                name: "FK_agreements_users_provider_id",
                table: "agreements",
                column: "provider_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_agreements_users_requester_id",
                table: "agreements",
                column: "requester_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_users_tip_from_user_id",
                table: "payments",
                column: "tip_from_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_users_tip_to_user_id",
                table: "payments",
                column: "tip_to_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agreements_users_provider_id",
                table: "agreements");

            migrationBuilder.DropForeignKey(
                name: "FK_agreements_users_requester_id",
                table: "agreements");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_users_tip_from_user_id",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_users_tip_to_user_id",
                table: "payments");

            migrationBuilder.RenameColumn(
                name: "tip_to_user_id",
                table: "payments",
                newName: "payer_id");

            migrationBuilder.RenameColumn(
                name: "tip_from_user_id",
                table: "payments",
                newName: "payee_id");

            migrationBuilder.RenameIndex(
                name: "IX_payments_tip_to_user_id",
                table: "payments",
                newName: "IX_payments_payer_id");

            migrationBuilder.RenameIndex(
                name: "IX_payments_tip_from_user_id",
                table: "payments",
                newName: "IX_payments_payee_id");

            migrationBuilder.RenameColumn(
                name: "requester_id",
                table: "agreements",
                newName: "seller_id");

            migrationBuilder.RenameColumn(
                name: "provider_id",
                table: "agreements",
                newName: "buyer_id");

            migrationBuilder.RenameIndex(
                name: "IX_agreements_requester_id",
                table: "agreements",
                newName: "IX_agreements_seller_id");

            migrationBuilder.RenameIndex(
                name: "IX_agreements_provider_id",
                table: "agreements",
                newName: "IX_agreements_buyer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_agreements_users_buyer_id",
                table: "agreements",
                column: "buyer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_agreements_users_seller_id",
                table: "agreements",
                column: "seller_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_users_payee_id",
                table: "payments",
                column: "payee_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_users_payer_id",
                table: "payments",
                column: "payer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
