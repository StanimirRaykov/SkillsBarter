using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class TokensAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PasswordResetTokenExpiry",
                table: "users",
                newName: "password_reset_token_expiry");

            migrationBuilder.RenameColumn(
                name: "PasswordResetToken",
                table: "users",
                newName: "password_reset_token");

            migrationBuilder.AddColumn<string>(
                name: "refresh_token",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "refresh_token_expiry",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "refresh_token",
                table: "users");

            migrationBuilder.DropColumn(
                name: "refresh_token_expiry",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "password_reset_token_expiry",
                table: "users",
                newName: "PasswordResetTokenExpiry");

            migrationBuilder.RenameColumn(
                name: "password_reset_token",
                table: "users",
                newName: "PasswordResetToken");
        }
    }
}
