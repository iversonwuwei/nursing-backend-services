using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingBackend.Services.Operations.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOperationsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    ActivityId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<string>(type: "text", nullable: false),
                    Time = table.Column<string>(type: "text", nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    Participants = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Teacher = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.ActivityId);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    EquipmentId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    SerialNumber = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PurchaseDate = table.Column<string>(type: "text", nullable: false),
                    MaintenanceDate = table.Column<string>(type: "text", nullable: false),
                    MaintenanceCycle = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    Room = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Signal = table.Column<int>(type: "integer", nullable: false),
                    Battery = table.Column<int>(type: "integer", nullable: false),
                    Uptime = table.Column<int>(type: "integer", nullable: false),
                    MetricsHr = table.Column<int>(type: "integer", nullable: false),
                    MetricsBp = table.Column<string>(type: "text", nullable: false),
                    MetricsTemp = table.Column<double>(type: "double precision", nullable: false),
                    MetricsSpo2 = table.Column<int>(type: "integer", nullable: false),
                    HistoryJson = table.Column<string>(type: "text", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptanceNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.EquipmentId);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    IncidentId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<string>(type: "text", nullable: false),
                    ElderName = table.Column<string>(type: "text", nullable: true),
                    Room = table.Column<string>(type: "text", nullable: false),
                    Reporter = table.Column<string>(type: "text", nullable: false),
                    ReporterRole = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    HandlingJson = table.Column<string>(type: "text", nullable: false),
                    NextStep = table.Column<string>(type: "text", nullable: true),
                    AttachmentsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StatusNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.IncidentId);
                });

            migrationBuilder.CreateTable(
                name: "Supplies",
                columns: table => new
                {
                    SupplyId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: false),
                    MinStock = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<string>(type: "text", nullable: false),
                    Supplier = table.Column<string>(type: "text", nullable: false),
                    Contact = table.Column<string>(type: "text", nullable: false),
                    LastPurchase = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "text", nullable: false),
                    HistoryJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IntakeNote = table.Column<string>(type: "text", nullable: true),
                    LastIntakeQuantity = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Supplies", x => x.SupplyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_TenantId_LifecycleStatus_Status_Date_Time",
                table: "Activities",
                columns: new[] { "TenantId", "LifecycleStatus", "Status", "Date", "Time" });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_TenantId_LifecycleStatus_Status_Category",
                table: "Equipment",
                columns: new[] { "TenantId", "LifecycleStatus", "Status", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_TenantId_Status_Level_OccurredAtUtc",
                table: "Incidents",
                columns: new[] { "TenantId", "Status", "Level", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Supplies_TenantId_LifecycleStatus_Status_Category",
                table: "Supplies",
                columns: new[] { "TenantId", "LifecycleStatus", "Status", "Category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "Supplies");
        }
    }
}
