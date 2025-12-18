using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestoneIdToDeliverable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MilestoneId",
                table: "deliverables",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_MilestoneId",
                table: "deliverables",
                column: "MilestoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_deliverables_milestones_MilestoneId",
                table: "deliverables",
                column: "MilestoneId",
                principalTable: "milestones",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_deliverables_milestones_MilestoneId",
                table: "deliverables");

            migrationBuilder.DropIndex(
                name: "IX_deliverables_MilestoneId",
                table: "deliverables");

            migrationBuilder.DropColumn(
                name: "MilestoneId",
                table: "deliverables");
        }
    }
}
