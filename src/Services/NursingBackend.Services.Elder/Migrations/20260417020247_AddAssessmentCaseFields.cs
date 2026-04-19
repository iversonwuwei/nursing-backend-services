using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingBackend.Services.Elder.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentCaseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdlScore",
                table: "Admissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AiAssessmentScore",
                table: "Admissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AiConfidence",
                table: "Admissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AiFocusTags",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiPlanTemplateCode",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiReasonSummary",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiReasons",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiRecommendedCareLevel",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AllergySummary",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssessmentStatus",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChronicConditions",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CognitiveLevel",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAtUtc",
                table: "Admissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedBy",
                table: "Admissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedCareLevel",
                table: "Admissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContact",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MedicationSummary",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestedCareLevel",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "Admissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskNotes",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceDocumentNames",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceLabel",
                table: "Admissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSummary",
                table: "Admissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "Admissions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdlScore",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "AiAssessmentScore",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "AiConfidence",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "AiFocusTags",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "AiPlanTemplateCode",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "AiReasonSummary",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "AiReasons",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "AiRecommendedCareLevel",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "AllergySummary",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "AssessmentStatus",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "ChronicConditions",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "CognitiveLevel",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "ConfirmedBy",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "ConfirmedCareLevel",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "EmergencyContact",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "MedicationSummary",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "RequestedCareLevel",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "RiskNotes",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "SourceDocumentNames",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "SourceLabel",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "SourceSummary",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Admissions");
        }
    }
}
