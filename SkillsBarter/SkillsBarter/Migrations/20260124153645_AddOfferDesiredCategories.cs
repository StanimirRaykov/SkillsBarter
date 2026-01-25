using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferDesiredCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "offer_desired_categories",
                columns: table => new
                {
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_code = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offer_desired_categories", x => new { x.offer_id, x.category_code });
                    table.ForeignKey(
                        name: "FK_offer_desired_categories_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_offer_desired_categories_skill_categories_category_code",
                        column: x => x.category_code,
                        principalTable: "skill_categories",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_offer_desired_categories_category_code",
                table: "offer_desired_categories",
                column: "category_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offer_desired_categories");
        }
    }
}
