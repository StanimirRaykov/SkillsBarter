using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class RenameMilestoneAmountToDurationInDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old amount column
            migrationBuilder.DropColumn(
                name: "amount",
                table: "milestones");

            // Add new duration_in_days column
            migrationBuilder.AddColumn<int>(
                name: "duration_in_days",
                table: "milestones",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            // Fix status column type (text to integer) - requires USING clause for PostgreSQL
            migrationBuilder.Sql(
                @"ALTER TABLE milestones
                  ALTER COLUMN status TYPE integer USING status::integer;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert status column type
            migrationBuilder.Sql(
                @"ALTER TABLE milestones
                  ALTER COLUMN status TYPE text USING status::text;");

            // Drop duration_in_days column
            migrationBuilder.DropColumn(
                name: "duration_in_days",
                table: "milestones");

            // Add back amount column
            migrationBuilder.AddColumn<decimal>(
                name: "amount",
                table: "milestones",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
