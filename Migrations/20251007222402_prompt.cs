using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TargetBrowse.Migrations
{
    /// <inheritdoc />
    public partial class prompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Summaries_PromptVersion",
                table: "Summaries");

            migrationBuilder.DropColumn(
                name: "ModelUsed",
                table: "Summaries");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "Summaries");

            migrationBuilder.DropColumn(
                name: "TokensUsed",
                table: "Summaries");

            migrationBuilder.AddColumn<Guid>(
                name: "AICallId",
                table: "Summaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModelEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CostPer1kInputTokens = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPer1kOutputTokens = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelEntity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserPromptTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxTokens = table.Column<int>(type: "int", nullable: true),
                    TopP = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptEntity_ModelEntity_ModelId",
                        column: x => x.ModelId,
                        principalTable: "ModelEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AICallEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ActualSystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActualUserPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Response = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AICallEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AICallEntity_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AICallEntity_PromptEntity_PromptId",
                        column: x => x.PromptId,
                        principalTable: "PromptEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Summaries_AICallId",
                table: "Summaries",
                column: "AICallId");

            migrationBuilder.CreateIndex(
                name: "IX_AICallEntity_PromptId",
                table: "AICallEntity",
                column: "PromptId");

            migrationBuilder.CreateIndex(
                name: "IX_AICallEntity_UserId",
                table: "AICallEntity",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptEntity_ModelId",
                table: "PromptEntity",
                column: "ModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Summaries_AICallEntity_AICallId",
                table: "Summaries",
                column: "AICallId",
                principalTable: "AICallEntity",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Summaries_AICallEntity_AICallId",
                table: "Summaries");

            migrationBuilder.DropTable(
                name: "AICallEntity");

            migrationBuilder.DropTable(
                name: "PromptEntity");

            migrationBuilder.DropTable(
                name: "ModelEntity");

            migrationBuilder.DropIndex(
                name: "IX_Summaries_AICallId",
                table: "Summaries");

            migrationBuilder.DropColumn(
                name: "AICallId",
                table: "Summaries");

            migrationBuilder.AddColumn<string>(
                name: "ModelUsed",
                table: "Summaries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                table: "Summaries",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TokensUsed",
                table: "Summaries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Summaries_PromptVersion",
                table: "Summaries",
                column: "PromptVersion");
        }
    }
}
