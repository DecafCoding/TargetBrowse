using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TargetBrowse.Migrations
{
    /// <inheritdoc />
    public partial class promptrename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AICallEntity_AspNetUsers_UserId",
                table: "AICallEntity");

            migrationBuilder.DropForeignKey(
                name: "FK_AICallEntity_PromptEntity_PromptId",
                table: "AICallEntity");

            migrationBuilder.DropForeignKey(
                name: "FK_PromptEntity_ModelEntity_ModelId",
                table: "PromptEntity");

            migrationBuilder.DropForeignKey(
                name: "FK_Summaries_AICallEntity_AICallId",
                table: "Summaries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PromptEntity",
                table: "PromptEntity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ModelEntity",
                table: "ModelEntity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AICallEntity",
                table: "AICallEntity");

            migrationBuilder.RenameTable(
                name: "PromptEntity",
                newName: "Prompts");

            migrationBuilder.RenameTable(
                name: "ModelEntity",
                newName: "Models");

            migrationBuilder.RenameTable(
                name: "AICallEntity",
                newName: "AICalls");

            migrationBuilder.RenameIndex(
                name: "IX_PromptEntity_ModelId",
                table: "Prompts",
                newName: "IX_Prompts_ModelId");

            migrationBuilder.RenameIndex(
                name: "IX_AICallEntity_UserId",
                table: "AICalls",
                newName: "IX_AICalls_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AICallEntity_PromptId",
                table: "AICalls",
                newName: "IX_AICalls_PromptId");

            migrationBuilder.AlterColumn<decimal>(
                name: "TopP",
                table: "Prompts",
                type: "decimal(3,2)",
                precision: 3,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Temperature",
                table: "Prompts",
                type: "decimal(3,2)",
                precision: 3,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CostPer1kOutputTokens",
                table: "Models",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CostPer1kInputTokens",
                table: "Models",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCost",
                table: "AICalls",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Prompts",
                table: "Prompts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Models",
                table: "Models",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AICalls",
                table: "AICalls",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_IsActive",
                table: "Prompts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_Name_Version",
                table: "Prompts",
                columns: new[] { "Name", "Version" },
                unique: true);

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
                name: "IX_AICalls_CreatedAt",
                table: "AICalls",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AICalls_Success",
                table: "AICalls",
                column: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_AICalls_UserId_CreatedAt",
                table: "AICalls",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_AICalls_AspNetUsers_UserId",
                table: "AICalls",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AICalls_Prompts_PromptId",
                table: "AICalls",
                column: "PromptId",
                principalTable: "Prompts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Prompts_Models_ModelId",
                table: "Prompts",
                column: "ModelId",
                principalTable: "Models",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
                name: "FK_AICalls_AspNetUsers_UserId",
                table: "AICalls");

            migrationBuilder.DropForeignKey(
                name: "FK_AICalls_Prompts_PromptId",
                table: "AICalls");

            migrationBuilder.DropForeignKey(
                name: "FK_Prompts_Models_ModelId",
                table: "Prompts");

            migrationBuilder.DropForeignKey(
                name: "FK_Summaries_AICalls_AICallId",
                table: "Summaries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Prompts",
                table: "Prompts");

            migrationBuilder.DropIndex(
                name: "IX_Prompts_IsActive",
                table: "Prompts");

            migrationBuilder.DropIndex(
                name: "IX_Prompts_Name_Version",
                table: "Prompts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Models",
                table: "Models");

            migrationBuilder.DropIndex(
                name: "IX_Models_IsActive",
                table: "Models");

            migrationBuilder.DropIndex(
                name: "IX_Models_Provider_Name",
                table: "Models");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AICalls",
                table: "AICalls");

            migrationBuilder.DropIndex(
                name: "IX_AICalls_CreatedAt",
                table: "AICalls");

            migrationBuilder.DropIndex(
                name: "IX_AICalls_Success",
                table: "AICalls");

            migrationBuilder.DropIndex(
                name: "IX_AICalls_UserId_CreatedAt",
                table: "AICalls");

            migrationBuilder.RenameTable(
                name: "Prompts",
                newName: "PromptEntity");

            migrationBuilder.RenameTable(
                name: "Models",
                newName: "ModelEntity");

            migrationBuilder.RenameTable(
                name: "AICalls",
                newName: "AICallEntity");

            migrationBuilder.RenameIndex(
                name: "IX_Prompts_ModelId",
                table: "PromptEntity",
                newName: "IX_PromptEntity_ModelId");

            migrationBuilder.RenameIndex(
                name: "IX_AICalls_UserId",
                table: "AICallEntity",
                newName: "IX_AICallEntity_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AICalls_PromptId",
                table: "AICallEntity",
                newName: "IX_AICallEntity_PromptId");

            migrationBuilder.AlterColumn<decimal>(
                name: "TopP",
                table: "PromptEntity",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(3,2)",
                oldPrecision: 3,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Temperature",
                table: "PromptEntity",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(3,2)",
                oldPrecision: 3,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CostPer1kOutputTokens",
                table: "ModelEntity",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "CostPer1kInputTokens",
                table: "ModelEntity",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCost",
                table: "AICallEntity",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PromptEntity",
                table: "PromptEntity",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ModelEntity",
                table: "ModelEntity",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AICallEntity",
                table: "AICallEntity",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AICallEntity_AspNetUsers_UserId",
                table: "AICallEntity",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AICallEntity_PromptEntity_PromptId",
                table: "AICallEntity",
                column: "PromptId",
                principalTable: "PromptEntity",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PromptEntity_ModelEntity_ModelId",
                table: "PromptEntity",
                column: "ModelId",
                principalTable: "ModelEntity",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Summaries_AICallEntity_AICallId",
                table: "Summaries",
                column: "AICallId",
                principalTable: "AICallEntity",
                principalColumn: "Id");
        }
    }
}
