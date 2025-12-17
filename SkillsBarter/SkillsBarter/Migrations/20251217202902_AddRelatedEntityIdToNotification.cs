using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class AddRelatedEntityIdToNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEntityId",
                table: "notifications",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "notifications");
        }
    }
}
