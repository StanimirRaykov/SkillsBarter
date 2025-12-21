using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "complainer_decision",
                table: "disputes",
                type: "text",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "respondent_decision",
                table: "disputes",
                type: "text",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "system_decision",
                table: "disputes",
                type: "text",
                nullable: false,
                defaultValue: "EscalateToModerator");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "complainer_decision",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "respondent_decision",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "system_decision",
                table: "disputes");
        }
    }
}
