using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingBackend.Services.Notification.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryAttempts",
                columns: table => new
                {
                    DeliveryAttemptId = table.Column<string>(type: "text", nullable: false),
                    NotificationId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    SourceService = table.Column<string>(type: "text", nullable: false),
                    SourceEntityId = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FailureCode = table.Column<string>(type: "text", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    CompensationStatus = table.Column<string>(type: "text", nullable: false),
                    CompensationReferenceId = table.Column<string>(type: "text", nullable: true),
                    AttemptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryAttempts", x => x.DeliveryAttemptId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAttempts_TenantId_NotificationId_AttemptedAtUtc",
                table: "DeliveryAttempts",
                columns: new[] { "TenantId", "NotificationId", "AttemptedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryAttempts");
        }
    }
}
