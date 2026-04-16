using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingBackend.Services.Elder.Migrations
{
    /// <inheritdoc />
    public partial class AddElderProfileEditableFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdlScore",
                table: "Elders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BirthDate",
                table: "Elders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CognitiveLevel",
                table: "Elders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ElderPhone",
                table: "Elders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityCard",
                table: "Elders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdlScore",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "CognitiveLevel",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "ElderPhone",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "IdentityCard",
                table: "Elders");
        }
    }
}
