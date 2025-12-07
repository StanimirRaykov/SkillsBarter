using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class SeedOfferStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "offer_status",
                columns: new[] { "code", "label" },
                values: new object[,]
                {
                    { "Active", "Active" },
                    { "Cancelled", "Cancelled" },
                    { "Completed", "Completed" },
                    { "UnderAgreement", "Under Agreement" },
                    { "UnderReview", "Under Review" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "offer_status",
                keyColumn: "code",
                keyValue: "Active");

            migrationBuilder.DeleteData(
                table: "offer_status",
                keyColumn: "code",
                keyValue: "Cancelled");

            migrationBuilder.DeleteData(
                table: "offer_status",
                keyColumn: "code",
                keyValue: "Completed");

            migrationBuilder.DeleteData(
                table: "offer_status",
                keyColumn: "code",
                keyValue: "UnderAgreement");

            migrationBuilder.DeleteData(
                table: "offer_status",
                keyColumn: "code",
                keyValue: "UnderReview");
        }
    }
}
