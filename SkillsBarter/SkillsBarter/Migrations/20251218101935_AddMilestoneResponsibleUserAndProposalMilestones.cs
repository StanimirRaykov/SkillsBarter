using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestoneResponsibleUserAndProposalMilestones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "proposed_milestones",
                table: "proposals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "responsible_user_id",
                table: "milestones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_milestones_responsible_user_id",
                table: "milestones",
                column: "responsible_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_milestones_users_responsible_user_id",
                table: "milestones",
                column: "responsible_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_milestones_users_responsible_user_id",
                table: "milestones");

            migrationBuilder.DropIndex(
                name: "IX_milestones_responsible_user_id",
                table: "milestones");

            migrationBuilder.DropColumn(
                name: "proposed_milestones",
                table: "proposals");

            migrationBuilder.DropColumn(
                name: "responsible_user_id",
                table: "milestones");
        }
    }
}
