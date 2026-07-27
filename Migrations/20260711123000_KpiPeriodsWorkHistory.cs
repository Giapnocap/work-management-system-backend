using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    public partial class KpiPeriodsWorkHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'[dbo].[Users]', N'JoinedUnitAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Users]
                    ADD [JoinedUnitAt] datetime2 NOT NULL
                        CONSTRAINT [DF_Users_JoinedUnitAt] DEFAULT SYSUTCDATETIME();
                END
            ");

            migrationBuilder.CreateTable(
                name: "KpiPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KpiPeriods_Users_LockedBy",
                        column: x => x.LockedBy,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserWorkHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWorkHistories_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserWorkHistories_Users_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserWorkHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KpiResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalTasks = table.Column<int>(type: "int", nullable: false),
                    CompletedOnTime = table.Column<int>(type: "int", nullable: false),
                    CompletedLate = table.Column<int>(type: "int", nullable: false),
                    OverdueTasks = table.Column<int>(type: "int", nullable: false),
                    RejectedReports = table.Column<int>(type: "int", nullable: false),
                    BonusPoints = table.Column<int>(type: "int", nullable: false),
                    PenaltyPoints = table.Column<int>(type: "int", nullable: false),
                    ReviewPenaltyPoints = table.Column<int>(type: "int", nullable: false),
                    UnitAverageScore = table.Column<double>(type: "float", nullable: false),
                    PersonalScore = table.Column<int>(type: "int", nullable: false),
                    IsManagerKpi = table.Column<bool>(type: "bit", nullable: false),
                    IsAtRisk = table.Column<bool>(type: "bit", nullable: false),
                    WarningMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KpiResults_KpiPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "KpiPeriods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KpiResults_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KpiResults_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_KpiPeriods_LockedBy",
                table: "KpiPeriods",
                column: "LockedBy");

            migrationBuilder.CreateIndex(
                name: "IX_KpiPeriods_StartDate_EndDate",
                table: "KpiPeriods",
                columns: new[] { "StartDate", "EndDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KpiResults_PeriodId_UserId",
                table: "KpiResults",
                columns: new[] { "PeriodId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KpiResults_UnitId",
                table: "KpiResults",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiResults_UserId",
                table: "KpiResults",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkHistories_ChangedBy",
                table: "UserWorkHistories",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkHistories_UnitId",
                table: "UserWorkHistories",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkHistories_UserId_EffectiveFrom",
                table: "UserWorkHistories",
                columns: new[] { "UserId", "EffectiveFrom" });

            migrationBuilder.Sql(@"
                DECLARE @now datetime2 = SYSUTCDATETIME();
                DECLARE @start datetime2 = DATEFROMPARTS(YEAR(@now), MONTH(@now), 1);
                DECLARE @end datetime2 = DATEADD(SECOND, -1, DATEADD(MONTH, 1, @start));

                IF NOT EXISTS (SELECT 1 FROM KpiPeriods)
                BEGIN
                    INSERT INTO KpiPeriods (Id, Name, Type, StartDate, EndDate, Status, CreatedAt)
                    VALUES (NEWID(), CONCAT('KPI ', FORMAT(@start, 'MM/yyyy')), 'Monthly', @start, @end, 'Open', @now);
                END

                INSERT INTO UserWorkHistories
                    (Id, UserId, UnitId, Role, EffectiveFrom, EffectiveTo, ChangedBy, ChangeReason, CreatedAt)
                SELECT NEWID(),
                       u.Id,
                       u.UnitId,
                       u.Role,
                       CASE
                           WHEN u.JoinedUnitAt IS NULL THEN @now
                           ELSE u.JoinedUnitAt
                       END,
                       NULL,
                       NULL,
                       'Initial migration',
                       @now
                FROM Users u
                WHERE u.IsApproved = 1
                  AND u.IsDeleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM UserWorkHistories h
                      WHERE h.UserId = u.Id
                        AND h.EffectiveTo IS NULL
                  );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "KpiResults");
            migrationBuilder.DropTable(name: "UserWorkHistories");
            migrationBuilder.DropTable(name: "KpiPeriods");
        }
    }
}
