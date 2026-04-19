using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingBackend.Services.Staffing.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationToStaffMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffMembers_TenantId_Department_Status_LifecycleStatus_Cre~",
                table: "StaffMembers");

            migrationBuilder.AddColumn<string>(
                name: "OrganizationId",
                table: "StaffMembers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrganizationName",
                table: "StaffMembers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_TenantId_OrganizationId_Department_Status_Life~",
                table: "StaffMembers",
                columns: new[] { "TenantId", "OrganizationId", "Department", "Status", "LifecycleStatus", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffMembers_TenantId_OrganizationId_Department_Status_Life~",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "OrganizationName",
                table: "StaffMembers");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_TenantId_Department_Status_LifecycleStatus_Cre~",
                table: "StaffMembers",
                columns: new[] { "TenantId", "Department", "Status", "LifecycleStatus", "CreatedAtUtc" });
        }
    }
}
