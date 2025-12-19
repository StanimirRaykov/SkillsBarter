using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class FixDeliverableUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_deliverables_agreement_id_submitted_by_id",
                table: "deliverables");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_agreement_id_MilestoneId",
                table: "deliverables",
                columns: new[] { "agreement_id", "MilestoneId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_deliverables_agreement_id_MilestoneId",
                table: "deliverables");

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_agreement_id_submitted_by_id",
                table: "deliverables",
                columns: new[] { "agreement_id", "submitted_by_id" },
                unique: true);
        }
    }
}
