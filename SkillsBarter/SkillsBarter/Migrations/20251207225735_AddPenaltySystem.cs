using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class AddPenaltySystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "penalties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agreement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false, defaultValue: "EUR"),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    charged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_penalties", x => x.id);
                    table.ForeignKey(
                        name: "FK_penalties_agreements_agreement_id",
                        column: x => x.agreement_id,
                        principalTable: "agreements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_penalties_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_penalties_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_penalties_agreement_id",
                table: "penalties",
                column: "agreement_id");

            migrationBuilder.CreateIndex(
                name: "IX_penalties_dispute_id",
                table: "penalties",
                column: "dispute_id");

            migrationBuilder.CreateIndex(
                name: "IX_penalties_status",
                table: "penalties",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_penalties_user_id",
                table: "penalties",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "penalties");
        }
    }
}
