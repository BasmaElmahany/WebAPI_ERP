using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAPI.Migrations.ProjectDb
{
    /// <inheritdoc />
    public partial class AddPhotoUrlToJournalEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only alter existing table
            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                schema: "dbo",
                table: "JournalEntries",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove column if rollback happens
            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                schema: "dbo",
                table: "JournalEntries");
        }
    }
}
