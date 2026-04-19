using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingBackend.Services.Staffing.Migrations
{
    /// <inheritdoc />
    public partial class InitialStaffingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffMembers",
                columns: table => new
                {
                    StaffId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Department = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmploymentSource = table.Column<string>(type: "text", nullable: false),
                    PartnerAgencyId = table.Column<string>(type: "text", nullable: true),
                    PartnerAgencyName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PartnerAffiliationRole = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Age = table.Column<int>(type: "integer", nullable: false),
                    Performance = table.Column<int>(type: "integer", nullable: false),
                    Attendance = table.Column<int>(type: "integer", nullable: false),
                    Satisfaction = table.Column<int>(type: "integer", nullable: false),
                    HireDate = table.Column<string>(type: "text", nullable: false),
                    ScheduleJson = table.Column<string>(type: "text", nullable: false),
                    CertificatesJson = table.Column<string>(type: "text", nullable: false),
                    Bonus = table.Column<string>(type: "text", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OnboardingNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMembers", x => x.StaffId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_TenantId_Department_Status_LifecycleStatus_Cre~",
                table: "StaffMembers",
                columns: new[] { "TenantId", "Department", "Status", "LifecycleStatus", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffMembers");
        }
    }
}
