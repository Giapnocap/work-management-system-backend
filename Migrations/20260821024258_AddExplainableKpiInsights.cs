using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    public partial class AddExplainableKpiInsights : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_KpiResults_NonNegative",
                table: "KpiResults");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualHours",
                table: "KpiResults",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CompletedTasks",
                table: "KpiResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FormulaVersion",
                table: "KpiResults",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "1.0");

            migrationBuilder.AddColumn<decimal>(
                name: "PlannedEffortHours",
                table: "KpiResults",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ProgressReportCount",
                table: "KpiResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE [KpiResults] SET [CompletedTasks] = [CompletedOnTime] + [CompletedLate], [ProgressReportCount] = [RejectedReports]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KpiResults_FormulaVersion",
                table: "KpiResults",
                sql: "LEN([FormulaVersion]) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KpiResults_Metric_Ranges",
                table: "KpiResults",
                sql: "[CompletedTasks] <= [TotalTasks] AND [CompletedOnTime] + [CompletedLate] <= [CompletedTasks] AND [OverdueTasks] <= [TotalTasks] AND [RejectedReports] <= [ProgressReportCount]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KpiResults_NonNegative",
                table: "KpiResults",
                sql: "[Score] >= 0 AND [TotalTasks] >= 0 AND [CompletedTasks] >= 0 AND [CompletedOnTime] >= 0 AND [CompletedLate] >= 0 AND [OverdueTasks] >= 0 AND [RejectedReports] >= 0 AND [ProgressReportCount] >= 0 AND [PlannedEffortHours] >= 0 AND [ActualHours] >= 0 AND [BonusPoints] >= 0 AND [PenaltyPoints] >= 0 AND [ReviewPenaltyPoints] >= 0 AND [UnitAverageScore] >= 0 AND [PersonalScore] >= 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_KpiResults_FormulaVersion",
                table: "KpiResults");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KpiResults_Metric_Ranges",
                table: "KpiResults");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KpiResults_NonNegative",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "ActualHours",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "CompletedTasks",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "FormulaVersion",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "PlannedEffortHours",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "ProgressReportCount",
                table: "KpiResults");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KpiResults_NonNegative",
                table: "KpiResults",
                sql: "[Score] >= 0 AND [TotalTasks] >= 0 AND [CompletedOnTime] >= 0 AND [CompletedLate] >= 0 AND [OverdueTasks] >= 0 AND [RejectedReports] >= 0 AND [BonusPoints] >= 0 AND [PenaltyPoints] >= 0 AND [ReviewPenaltyPoints] >= 0 AND [UnitAverageScore] >= 0 AND [PersonalScore] >= 0");
        }
    }
}
