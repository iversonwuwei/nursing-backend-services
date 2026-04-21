using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingBackend.Services.Health.Migrations
{
    /// <inheritdoc />
    public partial class AddVitalObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VitalObservations",
                columns: table => new
                {
                    ObservationId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    ElderId = table.Column<string>(type: "text", nullable: false),
                    BloodPressure = table.Column<string>(type: "text", nullable: false),
                    HeartRate = table.Column<int>(type: "integer", nullable: false),
                    Temperature = table.Column<decimal>(type: "numeric", nullable: false),
                    BloodSugar = table.Column<decimal>(type: "numeric", nullable: false),
                    Oxygen = table.Column<int>(type: "integer", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalObservations", x => x.ObservationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VitalObservations_TenantId_ElderId_RecordedAtUtc",
                table: "VitalObservations",
                columns: new[] { "TenantId", "ElderId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VitalObservations_TenantId_RecordedAtUtc",
                table: "VitalObservations",
                columns: new[] { "TenantId", "RecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VitalObservations");
        }
    }
}
