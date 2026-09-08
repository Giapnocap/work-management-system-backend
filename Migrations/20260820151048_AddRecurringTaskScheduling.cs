using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    public partial class AddRecurringTaskScheduling : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringTaskTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    RequiresReview = table.Column<bool>(type: "bit", nullable: false),
                    PlannedEffortHours = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RecurrenceType = table.Column<int>(type: "int", nullable: false),
                    Interval = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    DayOfMonth = table.Column<int>(type: "int", nullable: true),
                    NextRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastGeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringTaskTemplates", x => x.Id);
                    table.CheckConstraint("CK_RecurringTaskTemplates_Interval_Positive", "[Interval] >= 1");
                    table.CheckConstraint("CK_RecurringTaskTemplates_PlannedEffortHours_Positive", "[PlannedEffortHours] IS NULL OR [PlannedEffortHours] > 0");
                    table.CheckConstraint("CK_RecurringTaskTemplates_RecurrenceType_Range", "[RecurrenceType] >= 0 AND [RecurrenceType] <= 2");
                    table.CheckConstraint("CK_RecurringTaskTemplates_Schedule_Shape", "([RecurrenceType] = 0 AND [DayOfWeek] IS NULL AND [DayOfMonth] IS NULL) OR ([RecurrenceType] = 1 AND [DayOfWeek] BETWEEN 0 AND 6 AND [DayOfMonth] IS NULL) OR ([RecurrenceType] = 2 AND [DayOfWeek] IS NULL AND [DayOfMonth] BETWEEN 1 AND 31)");
                    table.ForeignKey(
                        name: "FK_RecurringTaskTemplates_Projects_ProjectId_UnitId",
                        columns: x => new { x.ProjectId, x.UnitId },
                        principalTable: "Projects",
                        principalColumns: new[] { "Id", "UnitId" });
                    table.ForeignKey(
                        name: "FK_RecurringTaskTemplates_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecurringTaskTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GeneratedTaskOccurrences",
                columns: table => new
                {
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledForUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedTaskOccurrences", x => new { x.TemplateId, x.ScheduledForUtc });
                    table.ForeignKey(
                        name: "FK_GeneratedTaskOccurrences_RecurringTaskTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "RecurringTaskTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GeneratedTaskOccurrences_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RecurringTaskAssignees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringTaskAssignees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringTaskAssignees_RecurringTaskTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "RecurringTaskTemplates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecurringTaskAssignees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedTaskOccurrences_TaskId",
                table: "GeneratedTaskOccurrences",
                column: "TaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTaskAssignees_TemplateId_UserId",
                table: "RecurringTaskAssignees",
                columns: new[] { "TemplateId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTaskAssignees_UserId_TemplateId",
                table: "RecurringTaskAssignees",
                columns: new[] { "UserId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTaskTemplates_CreatedByUserId",
                table: "RecurringTaskTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTaskTemplates_IsActive_NextRunAtUtc",
                table: "RecurringTaskTemplates",
                columns: new[] { "IsActive", "NextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTaskTemplates_ProjectId_UnitId",
                table: "RecurringTaskTemplates",
                columns: new[] { "ProjectId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTaskTemplates_UnitId",
                table: "RecurringTaskTemplates",
                column: "UnitId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneratedTaskOccurrences");

            migrationBuilder.DropTable(
                name: "RecurringTaskAssignees");

            migrationBuilder.DropTable(
                name: "RecurringTaskTemplates");
        }
    }
}
