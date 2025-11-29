using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class UpdateReviewToUseAgreementId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reviews_offers_offer_id",
                table: "reviews");

            migrationBuilder.RenameColumn(
                name: "offer_id",
                table: "reviews",
                newName: "agreement_id");

            migrationBuilder.RenameIndex(
                name: "IX_reviews_offer_id",
                table: "reviews",
                newName: "IX_reviews_agreement_id");

            migrationBuilder.AddForeignKey(
                name: "FK_reviews_agreements_agreement_id",
                table: "reviews",
                column: "agreement_id",
                principalTable: "agreements",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reviews_agreements_agreement_id",
                table: "reviews");

            migrationBuilder.RenameColumn(
                name: "agreement_id",
                table: "reviews",
                newName: "offer_id");

            migrationBuilder.RenameIndex(
                name: "IX_reviews_agreement_id",
                table: "reviews",
                newName: "IX_reviews_offer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_reviews_offers_offer_id",
                table: "reviews",
                column: "offer_id",
                principalTable: "offers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
