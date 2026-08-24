using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskTimelineReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UploadFiles_TaskId",
                table: "UploadFiles");

            migrationBuilder.DropIndex(
                name: "IX_TaskHistories_TaskId_ChangedAt",
                table: "TaskHistories");

            migrationBuilder.DropIndex(
                name: "IX_Progresses_TaskId_UpdatedAt",
                table: "Progresses");

            migrationBuilder.CreateIndex(
                name: "IX_UploadFiles_TaskId_CreatedAt_Id",
                table: "UploadFiles",
                columns: new[] { "TaskId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskHistories_TaskId_ChangedAt_Id",
                table: "TaskHistories",
                columns: new[] { "TaskId", "ChangedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_TaskId_CreatedAt_Id",
                table: "TaskComments",
                columns: new[] { "TaskId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_TaskId_SentAtUtc_Id",
                table: "ScheduledNotifications",
                columns: new[] { "TaskId", "SentAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Progresses_TaskId_UpdatedAt_Id",
                table: "Progresses",
                columns: new[] { "TaskId", "UpdatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UploadFiles_TaskId_CreatedAt_Id",
                table: "UploadFiles");

            migrationBuilder.DropIndex(
                name: "IX_TaskHistories_TaskId_ChangedAt_Id",
                table: "TaskHistories");

            migrationBuilder.DropIndex(
                name: "IX_TaskComments_TaskId_CreatedAt_Id",
                table: "TaskComments");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledNotifications_TaskId_SentAtUtc_Id",
                table: "ScheduledNotifications");

            migrationBuilder.DropIndex(
                name: "IX_Progresses_TaskId_UpdatedAt_Id",
                table: "Progresses");

            migrationBuilder.CreateIndex(
                name: "IX_UploadFiles_TaskId",
                table: "UploadFiles",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskHistories_TaskId_ChangedAt",
                table: "TaskHistories",
                columns: new[] { "TaskId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Progresses_TaskId_UpdatedAt",
                table: "Progresses",
                columns: new[] { "TaskId", "UpdatedAt" });
        }
    }
}
