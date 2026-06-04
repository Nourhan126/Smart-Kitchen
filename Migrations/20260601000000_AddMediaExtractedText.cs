using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartKitchen.API.Migrations
{
    public partial class AddMediaExtractedText : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExtractedAt",
                table: "MediaAssets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "MediaAssets",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally non-destructive. Uploaded file indexes should not
            // be dropped during rollback because they are user data.
        }
    }
}
