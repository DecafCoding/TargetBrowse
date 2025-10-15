using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TargetBrowse.Migrations
{
    /// <inheritdoc />
    public partial class summarytables : Migration
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
                name: "Models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CostPer1kInputTokens = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CostPer1kOutputTokens = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserPromptTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: true),
                    MaxTokens = table.Column<int>(type: "int", nullable: true),
                    TopP = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_Prompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prompts_Models_ModelId",
                        column: x => x.ModelId,
                        principalTable: "Models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AICalls",
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
                    TotalCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
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
                    table.PrimaryKey("PK_AICalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AICalls_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AICalls_Prompts_PromptId",
                        column: x => x.PromptId,
                        principalTable: "Prompts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Summaries_AICallId",
                table: "Summaries",
                column: "AICallId");

            migrationBuilder.CreateIndex(
                name: "IX_AICalls_CreatedAt",
                table: "AICalls",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AICalls_PromptId",
                table: "AICalls",
                column: "PromptId");

            migrationBuilder.CreateIndex(
                name: "IX_AICalls_Success",
                table: "AICalls",
                column: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_AICalls_UserId",
                table: "AICalls",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AICalls_UserId_CreatedAt",
                table: "AICalls",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Models_IsActive",
                table: "Models",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Models_Provider_Name",
                table: "Models",
                columns: new[] { "Provider", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_IsActive",
                table: "Prompts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_ModelId",
                table: "Prompts",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_Name_Version",
                table: "Prompts",
                columns: new[] { "Name", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Summaries_AICalls_AICallId",
                table: "Summaries",
                column: "AICallId",
                principalTable: "AICalls",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Summaries_AICalls_AICallId",
                table: "Summaries");

            migrationBuilder.DropTable(
                name: "AICalls");

            migrationBuilder.DropTable(
                name: "Prompts");

            migrationBuilder.DropTable(
                name: "Models");

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
