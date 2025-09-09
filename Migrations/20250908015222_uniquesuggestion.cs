using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TargetBrowse.Migrations
{
    /// <inheritdoc />
    public partial class uniquesuggestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Suggestions_UserId_VideoId",
                table: "Suggestions");

            migrationBuilder.CreateIndex(
                name: "IX_Suggestions_UserId_VideoId",
                table: "Suggestions",
                columns: new[] { "UserId", "VideoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Suggestions_UserId_VideoId",
                table: "Suggestions");

            migrationBuilder.CreateIndex(
                name: "IX_Suggestions_UserId_VideoId",
                table: "Suggestions",
                columns: new[] { "UserId", "VideoId" },
                unique: true);
        }
    }
}
