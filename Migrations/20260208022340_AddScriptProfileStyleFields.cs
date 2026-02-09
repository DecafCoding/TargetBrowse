using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TargetBrowse.Migrations
{
    /// <inheritdoc />
    public partial class AddScriptProfileStyleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudienceRelationship",
                table: "UserScriptProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Insider-Outsider");

            migrationBuilder.AddColumn<string>(
                name: "HookStrategy",
                table: "UserScriptProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Insider Secret");

            migrationBuilder.AddColumn<string>(
                name: "InformationDensity",
                table: "UserScriptProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "High Density");

            migrationBuilder.AddColumn<string>(
                name: "RhetoricalStyle",
                table: "UserScriptProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Extended Metaphors");

            migrationBuilder.AddColumn<string>(
                name: "StructureStyle",
                table: "UserScriptProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Enumerated Scaffolding");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudienceRelationship",
                table: "UserScriptProfiles");

            migrationBuilder.DropColumn(
                name: "HookStrategy",
                table: "UserScriptProfiles");

            migrationBuilder.DropColumn(
                name: "InformationDensity",
                table: "UserScriptProfiles");

            migrationBuilder.DropColumn(
                name: "RhetoricalStyle",
                table: "UserScriptProfiles");

            migrationBuilder.DropColumn(
                name: "StructureStyle",
                table: "UserScriptProfiles");
        }
    }
}
