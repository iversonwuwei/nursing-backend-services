using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingBackend.Services.Rooms.Migrations
{
    /// <inheritdoc />
    public partial class InitialRoomsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    RoomId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Floor = table.Column<int>(type: "integer", nullable: false),
                    FloorName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OrganizationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OrganizationName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FacilitiesJson = table.Column<string>(type: "text", nullable: false),
                    CleanStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastClean = table.Column<string>(type: "text", nullable: false),
                    NextClean = table.Column<string>(type: "text", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActivationNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.RoomId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_TenantId_Status_LifecycleStatus_OrganizationName_Crea~",
                table: "Rooms",
                columns: new[] { "TenantId", "Status", "LifecycleStatus", "OrganizationName", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rooms");
        }
    }
}
