using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms = table.Column<string>(type: "text", nullable: false),
                    proposer_offer = table.Column<string>(type: "text", nullable: false),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "PendingOfferOwnerReview"),
                    pending_response_from_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modification_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_modified_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decline_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    agreement_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposals", x => x.id);
                    table.ForeignKey(
                        name: "FK_proposals_agreements_agreement_id",
                        column: x => x.agreement_id,
                        principalTable: "agreements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_proposals_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_proposals_users_last_modified_by_user_id",
                        column: x => x.last_modified_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposals_users_offer_owner_id",
                        column: x => x.offer_owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposals_users_pending_response_from_user_id",
                        column: x => x.pending_response_from_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_proposals_users_proposer_id",
                        column: x => x.proposer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "proposal_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    terms = table.Column<string>(type: "text", nullable: false),
                    proposer_offer = table.Column<string>(type: "text", nullable: false),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposal_histories", x => x.id);
                    table.ForeignKey(
                        name: "FK_proposal_histories_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_proposal_histories_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_proposal_histories_actor_id",
                table: "proposal_histories",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_histories_proposal_id",
                table: "proposal_histories",
                column: "proposal_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposal_histories_proposal_id_created_at",
                table: "proposal_histories",
                columns: new[] { "proposal_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_proposals_agreement_id",
                table: "proposals",
                column: "agreement_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_last_modified_by_user_id",
                table: "proposals",
                column: "last_modified_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_offer_id",
                table: "proposals",
                column: "offer_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_offer_id_status",
                table: "proposals",
                columns: new[] { "offer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_proposals_offer_owner_id",
                table: "proposals",
                column: "offer_owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_pending_response_from_user_id",
                table: "proposals",
                column: "pending_response_from_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_proposer_id",
                table: "proposals",
                column: "proposer_id");

            migrationBuilder.CreateIndex(
                name: "IX_proposals_status",
                table: "proposals",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "proposal_histories");

            migrationBuilder.DropTable(
                name: "proposals");
        }
    }
}
