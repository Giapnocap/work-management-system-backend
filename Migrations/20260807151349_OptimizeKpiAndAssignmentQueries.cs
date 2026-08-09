using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeKpiAndAssignmentQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserWorkHistories_UnitId",
                table: "UserWorkHistories");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignees_UnitId",
                table: "TaskAssignees");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignees_UserId",
                table: "TaskAssignees");

            migrationBuilder.DropIndex(
                name: "IX_Progresses_TaskId",
                table: "Progresses");

            migrationBuilder.DropIndex(
                name: "IX_Progresses_UserId",
                table: "Progresses");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkHistories_UnitId_EffectiveFrom",
                table: "UserWorkHistories",
                columns: new[] { "UnitId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignees_UnitId_TaskId",
                table: "TaskAssignees",
                columns: new[] { "UnitId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignees_UserId_TaskId",
                table: "TaskAssignees",
                columns: new[] { "UserId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_Progresses_TaskId_UpdatedAt",
                table: "Progresses",
                columns: new[] { "TaskId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Progresses_UserId_Status_UpdatedAt_TaskId",
                table: "Progresses",
                columns: new[] { "UserId", "Status", "UpdatedAt", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_KpiResults_PeriodId_UnitId",
                table: "KpiResults",
                columns: new[] { "PeriodId", "UnitId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserWorkHistories_UnitId_EffectiveFrom",
                table: "UserWorkHistories");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignees_UnitId_TaskId",
                table: "TaskAssignees");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignees_UserId_TaskId",
                table: "TaskAssignees");

            migrationBuilder.DropIndex(
                name: "IX_Progresses_TaskId_UpdatedAt",
                table: "Progresses");

            migrationBuilder.DropIndex(
                name: "IX_Progresses_UserId_Status_UpdatedAt_TaskId",
                table: "Progresses");

            migrationBuilder.DropIndex(
                name: "IX_KpiResults_PeriodId_UnitId",
                table: "KpiResults");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkHistories_UnitId",
                table: "UserWorkHistories",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignees_UnitId",
                table: "TaskAssignees",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignees_UserId",
                table: "TaskAssignees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Progresses_TaskId",
                table: "Progresses",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Progresses_UserId",
                table: "Progresses",
                column: "UserId");
        }
    }
}
