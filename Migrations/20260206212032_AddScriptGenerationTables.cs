using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TargetBrowse.Migrations
{
    /// <inheritdoc />
    public partial class AddScriptGenerationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScriptContents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScriptStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AnalysisJsonResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainTopic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CohesionScore = table.Column<int>(type: "int", nullable: true),
                    OutlineJsonStructure = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedLengthMinutes = table.Column<int>(type: "int", nullable: true),
                    TargetLengthMinutes = table.Column<int>(type: "int", nullable: true),
                    ScriptText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WordCount = table.Column<int>(type: "int", nullable: false),
                    EstimatedDurationSeconds = table.Column<int>(type: "int", nullable: false),
                    InternalNotesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VideoCount = table.Column<int>(type: "int", nullable: false),
                    AnalysisAICallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutlineAICallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScriptAICallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScriptContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScriptContents_AICalls_AnalysisAICallId",
                        column: x => x.AnalysisAICallId,
                        principalTable: "AICalls",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScriptContents_AICalls_OutlineAICallId",
                        column: x => x.OutlineAICallId,
                        principalTable: "AICalls",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScriptContents_AICalls_ScriptAICallId",
                        column: x => x.ScriptAICallId,
                        principalTable: "AICalls",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ScriptContents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserScriptProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Tone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Pacing = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Complexity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserScriptProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserScriptProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScriptContents_AnalysisAICallId",
                table: "ScriptContents",
                column: "AnalysisAICallId");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptContents_GeneratedAt",
                table: "ScriptContents",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptContents_OutlineAICallId",
                table: "ScriptContents",
                column: "OutlineAICallId");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptContents_ProjectId",
                table: "ScriptContents",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScriptContents_ScriptAICallId",
                table: "ScriptContents",
                column: "ScriptAICallId");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptContents_ScriptStatus",
                table: "ScriptContents",
                column: "ScriptStatus");

            migrationBuilder.CreateIndex(
                name: "IX_UserScriptProfiles_UserId",
                table: "UserScriptProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScriptContents");

            migrationBuilder.DropTable(
                name: "UserScriptProfiles");
        }
    }
}
