using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDeadlineReminderScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReminderPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BeforeDueHours = table.Column<int>(type: "int", nullable: false),
                    OverdueEscalationHours = table.Column<int>(type: "int", nullable: false),
                    NotifyAssignee = table.Column<bool>(type: "bit", nullable: false),
                    NotifyManager = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderPolicies", x => x.Id);
                    table.CheckConstraint("CK_ReminderPolicies_BeforeDueHours_Range", "[BeforeDueHours] >= 1 AND [BeforeDueHours] <= 720");
                    table.CheckConstraint("CK_ReminderPolicies_OverdueEscalationHours_Range", "[OverdueEscalationHours] >= 0 AND [OverdueEscalationHours] <= 720");
                    table.CheckConstraint("CK_ReminderPolicies_Scope_Shape", "([ScopeType] = 0 AND [UnitId] IS NULL AND [ProjectId] IS NULL) OR ([ScopeType] = 1 AND [UnitId] IS NOT NULL AND [ProjectId] IS NULL) OR ([ScopeType] = 2 AND [UnitId] IS NULL AND [ProjectId] IS NOT NULL)");
                    table.CheckConstraint("CK_ReminderPolicies_ScopeType_Range", "[ScopeType] >= 0 AND [ScopeType] <= 2");
                    table.ForeignKey(
                        name: "FK_ReminderPolicies_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReminderPolicies_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ScheduledNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ScheduledForUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledNotifications", x => x.Id);
                    table.CheckConstraint("CK_ScheduledNotifications_RetryCount_NonNegative", "[RetryCount] >= 0");
                    table.CheckConstraint("CK_ScheduledNotifications_Status_Range", "[Status] >= 0 AND [Status] <= 3");
                    table.CheckConstraint("CK_ScheduledNotifications_Type_Range", "[Type] >= 0 AND [Type] <= 2");
                    table.ForeignKey(
                        name: "FK_ScheduledNotifications_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "ReminderPolicies",
                columns: new[]
                {
                    "Id",
                    "ScopeType",
                    "UnitId",
                    "ProjectId",
                    "BeforeDueHours",
                    "OverdueEscalationHours",
                    "NotifyAssignee",
                    "NotifyManager",
                    "IsActive",
                    "CreatedAtUtc",
                    "UpdatedAtUtc"
                },
                values: new object[]
                {
                    new Guid("a938d19a-32bb-4ccf-9a5c-a412e8e64bdd"),
                    0,
                    null,
                    null,
                    24,
                    24,
                    true,
                    true,
                    true,
                    new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderPolicies_ProjectId",
                table: "ReminderPolicies",
                column: "ProjectId",
                unique: true,
                filter: "[ScopeType] = 2 AND [ProjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderPolicies_ScopeType",
                table: "ReminderPolicies",
                column: "ScopeType",
                unique: true,
                filter: "[ScopeType] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderPolicies_UnitId",
                table: "ReminderPolicies",
                column: "UnitId",
                unique: true,
                filter: "[ScopeType] = 1 AND [UnitId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_EventKey",
                table: "ScheduledNotifications",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_Status_ScheduledForUtc_RetryCount",
                table: "ScheduledNotifications",
                columns: new[] { "Status", "ScheduledForUtc", "RetryCount" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_TaskId_ScheduledForUtc",
                table: "ScheduledNotifications",
                columns: new[] { "TaskId", "ScheduledForUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_TaskId_Type",
                table: "ScheduledNotifications",
                columns: new[] { "TaskId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReminderPolicies");

            migrationBuilder.DropTable(
                name: "ScheduledNotifications");
        }
    }
}
