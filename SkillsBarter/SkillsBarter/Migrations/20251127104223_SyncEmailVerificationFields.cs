using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillsBarter.Migrations
{
    /// <inheritdoc />
    public partial class SyncEmailVerificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Columns already exist in database from previous migration
            // This migration only updates the model snapshot
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No changes to revert
        }
    }
}
