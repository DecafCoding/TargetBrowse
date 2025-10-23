using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TargetBrowse.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VideoTypeId",
                table: "Videos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VideoTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Videos_VideoTypeId",
                table: "Videos",
                column: "VideoTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoTypes_Code",
                table: "VideoTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoTypes_Name",
                table: "VideoTypes",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_VideoTypes_VideoTypeId",
                table: "Videos",
                column: "VideoTypeId",
                principalTable: "VideoTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Videos_VideoTypes_VideoTypeId",
                table: "Videos");

            migrationBuilder.DropTable(
                name: "VideoTypes");

            migrationBuilder.DropIndex(
                name: "IX_Videos_VideoTypeId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "VideoTypeId",
                table: "Videos");
        }
    }
}
