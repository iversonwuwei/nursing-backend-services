using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingBackend.Services.Care.Migrations
{
    /// <inheritdoc />
    public partial class AddCareWorkflowRealization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CareWorkflowAudits",
                columns: table => new
                {
                    AuditId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    AggregateType = table.Column<string>(type: "text", nullable: false),
                    AggregateId = table.Column<string>(type: "text", nullable: false),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    OperatorUserId = table.Column<string>(type: "text", nullable: false),
                    OperatorUserName = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    DetailJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareWorkflowAudits", x => x.AuditId);
                });

            migrationBuilder.CreateTable(
                name: "ServicePackages",
                columns: table => new
                {
                    PackageId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CareLevel = table.Column<string>(type: "text", nullable: false),
                    TargetGroup = table.Column<string>(type: "text", nullable: false),
                    MonthlyPrice = table.Column<string>(type: "text", nullable: false),
                    SettlementCycle = table.Column<string>(type: "text", nullable: false),
                    ServiceScopeJson = table.Column<string>(type: "text", nullable: false),
                    AddOnsJson = table.Column<string>(type: "text", nullable: false),
                    BoundElders = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PricingNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePackages", x => x.PackageId);
                });

            migrationBuilder.CreateTable(
                name: "ServicePlanAssignments",
                columns: table => new
                {
                    AssignmentId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    PlanId = table.Column<string>(type: "text", nullable: false),
                    ElderlyName = table.Column<string>(type: "text", nullable: false),
                    PackageName = table.Column<string>(type: "text", nullable: false),
                    Room = table.Column<string>(type: "text", nullable: false),
                    StaffName = table.Column<string>(type: "text", nullable: false),
                    StaffRole = table.Column<string>(type: "text", nullable: false),
                    EmploymentSource = table.Column<string>(type: "text", nullable: false),
                    PartnerAgencyName = table.Column<string>(type: "text", nullable: true),
                    DayLabel = table.Column<string>(type: "text", nullable: false),
                    Shift = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePlanAssignments", x => x.AssignmentId);
                });

            migrationBuilder.CreateTable(
                name: "ServicePlans",
                columns: table => new
                {
                    PlanId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    PackageId = table.Column<string>(type: "text", nullable: false),
                    PackageName = table.Column<string>(type: "text", nullable: false),
                    ElderlyName = table.Column<string>(type: "text", nullable: false),
                    Room = table.Column<string>(type: "text", nullable: false),
                    CareLevel = table.Column<string>(type: "text", nullable: false),
                    Focus = table.Column<string>(type: "text", nullable: false),
                    ShiftSummary = table.Column<string>(type: "text", nullable: false),
                    OwnerRole = table.Column<string>(type: "text", nullable: false),
                    OwnerName = table.Column<string>(type: "text", nullable: false),
                    RiskTagsJson = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePlans", x => x.PlanId);
                });

            migrationBuilder.CreateTable(
                name: "ServicePlanTaskExecutions",
                columns: table => new
                {
                    TaskExecutionId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    PlanId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    HandledBy = table.Column<string>(type: "text", nullable: true),
                    HandledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActionNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePlanTaskExecutions", x => x.TaskExecutionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareWorkflowAudits_TenantId_AggregateType_AggregateId",
                table: "CareWorkflowAudits",
                columns: new[] { "TenantId", "AggregateType", "AggregateId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePackages_TenantId_Status",
                table: "ServicePackages",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePlanAssignments_TenantId_DayLabel_StaffName",
                table: "ServicePlanAssignments",
                columns: new[] { "TenantId", "DayLabel", "StaffName" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePlans_TenantId_Status",
                table: "ServicePlans",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ServicePlanTaskExecutions_TenantId_PlanId",
                table: "ServicePlanTaskExecutions",
                columns: new[] { "TenantId", "PlanId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareWorkflowAudits");

            migrationBuilder.DropTable(
                name: "ServicePackages");

            migrationBuilder.DropTable(
                name: "ServicePlanAssignments");

            migrationBuilder.DropTable(
                name: "ServicePlans");

            migrationBuilder.DropTable(
                name: "ServicePlanTaskExecutions");
        }
    }
}
