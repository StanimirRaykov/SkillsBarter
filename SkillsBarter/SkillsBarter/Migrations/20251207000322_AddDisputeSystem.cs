using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class AddDisputeSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "disputes",
                type: "text",
                nullable: false,
                defaultValue: "Open",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "complainer_approved_before_dispute",
                table: "disputes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "complainer_delivered",
                table: "disputes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "complainer_on_time",
                table: "disputes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "disputes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "escalated_at",
                table: "disputes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "moderator_id",
                table: "disputes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "moderator_notes",
                table: "disputes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolution",
                table: "disputes",
                type: "text",
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<bool>(
                name: "respondent_approved_before_dispute",
                table: "disputes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "respondent_delivered",
                table: "disputes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "respondent_id",
                table: "disputes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "respondent_on_time",
                table: "disputes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "response_deadline",
                table: "disputes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "response_received_at",
                table: "disputes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "score",
                table: "disputes",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.CreateTable(
                name: "dispute_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_evidence", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispute_evidence_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispute_evidence_users_submitted_by_id",
                        column: x => x.submitted_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_disputes_moderator_id",
                table: "disputes",
                column: "moderator_id");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_respondent_id",
                table: "disputes",
                column: "respondent_id");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_status",
                table: "disputes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_status_response_deadline",
                table: "disputes",
                columns: new[] { "status", "response_deadline" });

            migrationBuilder.CreateIndex(
                name: "IX_dispute_evidence_dispute_id",
                table: "dispute_evidence",
                column: "dispute_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispute_evidence_submitted_by_id",
                table: "dispute_evidence",
                column: "submitted_by_id");

            migrationBuilder.AddForeignKey(
                name: "FK_disputes_users_moderator_id",
                table: "disputes",
                column: "moderator_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_disputes_users_respondent_id",
                table: "disputes",
                column: "respondent_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_disputes_users_moderator_id",
                table: "disputes");

            migrationBuilder.DropForeignKey(
                name: "FK_disputes_users_respondent_id",
                table: "disputes");

            migrationBuilder.DropTable(
                name: "dispute_evidence");

            migrationBuilder.DropIndex(
                name: "IX_disputes_moderator_id",
                table: "disputes");

            migrationBuilder.DropIndex(
                name: "IX_disputes_respondent_id",
                table: "disputes");

            migrationBuilder.DropIndex(
                name: "IX_disputes_status",
                table: "disputes");

            migrationBuilder.DropIndex(
                name: "IX_disputes_status_response_deadline",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "complainer_approved_before_dispute",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "complainer_delivered",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "complainer_on_time",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "description",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "escalated_at",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "moderator_id",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "moderator_notes",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "resolution",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "respondent_approved_before_dispute",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "respondent_delivered",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "respondent_id",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "respondent_on_time",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "response_deadline",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "response_received_at",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "score",
                table: "disputes");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "disputes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Open");
        }
    }
}
