using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingBackend.Services.Elder.Migrations
{
    /// <inheritdoc />
    public partial class AddElderEntrustmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EntrustmentOrganization",
                table: "Elders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntrustmentType",
                table: "Elders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlySubsidy",
                table: "Elders",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceItems",
                table: "Elders",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ServiceNotes",
                table: "Elders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntrustmentOrganization",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "EntrustmentType",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "MonthlySubsidy",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "ServiceItems",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "ServiceNotes",
                table: "Elders");
        }
    }
}
