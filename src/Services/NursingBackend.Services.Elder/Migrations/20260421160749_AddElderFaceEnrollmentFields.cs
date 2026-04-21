using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingBackend.Services.Elder.Migrations
{
    /// <inheritdoc />
    public partial class AddElderFaceEnrollmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FaceActivatedAtUtc",
                table: "Elders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaceActivationNote",
                table: "Elders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaceCapturedSteps",
                table: "Elders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FaceDeviceLabel",
                table: "Elders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaceEnrollmentStatus",
                table: "Elders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FaceEntrySource",
                table: "Elders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FaceLastUpdatedUtc",
                table: "Elders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaceOperator",
                table: "Elders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FaceQualityScore",
                table: "Elders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FaceQualitySummary",
                table: "Elders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FaceRetakeReason",
                table: "Elders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaceActivatedAtUtc",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "FaceActivationNote",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "FaceCapturedSteps",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "FaceDeviceLabel",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "FaceEnrollmentStatus",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "FaceEntrySource",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "FaceLastUpdatedUtc",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "FaceOperator",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "FaceQualityScore",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "FaceQualitySummary",
                table: "Elders");

            migrationBuilder.DropColumn(
                name: "FaceRetakeReason",
                table: "Elders");
        }
    }
}
