using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class ProductivityProjectBoardUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Reviews_ProgressId'
                      AND object_id = OBJECT_ID(N'[dbo].[Reviews]')
                )
                BEGIN
                    DROP INDEX [IX_Reviews_ProgressId] ON [Reviews];
                END
            ");

            migrationBuilder.Sql(@"
                ;WITH DuplicateCommentSeens AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY CommentId, UserId ORDER BY SeenAt DESC, Id DESC) AS rn
                    FROM CommentSeens
                )
                DELETE FROM CommentSeens
                WHERE Id IN (SELECT Id FROM DuplicateCommentSeens WHERE rn > 1);

                ;WITH DuplicateCommentReactions AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY CommentId, UserId ORDER BY CreatedAt DESC, Id DESC) AS rn
                    FROM CommentReactions
                )
                DELETE FROM CommentReactions
                WHERE Id IN (SELECT Id FROM DuplicateCommentReactions WHERE rn > 1);

                ;WITH DuplicateReviews AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY ProgressId ORDER BY ReviewedAt DESC, Id DESC) AS rn
                    FROM Reviews
                )
                DELETE FROM Reviews
                WHERE Id IN (SELECT Id FROM DuplicateReviews WHERE rn > 1);

                DELETE unitAssignee
                FROM TaskAssignees unitAssignee
                WHERE unitAssignee.UnitId IS NOT NULL
                  AND EXISTS (
                      SELECT 1
                      FROM TaskAssignees directAssignee
                      WHERE directAssignee.TaskId = unitAssignee.TaskId
                        AND directAssignee.UserId IS NOT NULL
                  );

                ;WITH DuplicateUnitAssignees AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY TaskId, UnitId ORDER BY Id DESC) AS rn
                    FROM TaskAssignees
                    WHERE UnitId IS NOT NULL
                )
                DELETE FROM TaskAssignees
                WHERE Id IN (SELECT Id FROM DuplicateUnitAssignees WHERE rn > 1);

                ;WITH DuplicateUserAssignees AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY TaskId, UserId ORDER BY Id DESC) AS rn
                    FROM TaskAssignees
                    WHERE UserId IS NOT NULL
                )
                DELETE FROM TaskAssignees
                WHERE Id IN (SELECT Id FROM DuplicateUserAssignees WHERE rn > 1);

                DELETE FROM TaskAssignees
                WHERE (UserId IS NULL AND UnitId IS NULL)
                   OR (UserId IS NOT NULL AND UnitId IS NOT NULL);

                UPDATE Progresses SET [Percent] = 0 WHERE [Percent] < 0;
                UPDATE Progresses SET [Percent] = 100 WHERE [Percent] > 100;
                UPDATE Progresses SET HoursSpent = 0 WHERE HoursSpent < 0;
                UPDATE Tasks SET EstimatedHours = 0 WHERE EstimatedHours < 0;
                UPDATE Tasks SET ActualHours = 0 WHERE ActualHours < 0;

                UPDATE t
                SET ActualHours = ISNULL(p.TotalApprovedHours, 0)
                FROM Tasks t
                OUTER APPLY (
                    SELECT SUM(HoursSpent) AS TotalApprovedHours
                    FROM Progresses p
                    WHERE p.TaskId = t.Id AND p.Status = 3
                ) p;
            ");

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedBy",
                table: "UploadFiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE uf
                SET UploadedBy = COALESCE(p.UserId, t.CreatedBy)
                FROM UploadFiles uf
                LEFT JOIN Progresses p ON p.Id = uf.ProgressId
                LEFT JOIN Tasks t ON t.Id = uf.TaskId
                WHERE uf.UploadedBy IS NULL;
            ");

            migrationBuilder.AddColumn<Guid>(
                name: "BoardId",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ColumnId",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Tasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletedBy",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentTaskId",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Tasks",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresReview",
                table: "Tasks",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Tasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projects_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TaskActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskActivities_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskActivities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskReminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemindAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskReminders_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskReminders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Boards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Boards_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardColumns_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_BoardId",
                table: "Tasks",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ColumnId",
                table: "Tasks",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks",
                column: "ProjectId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tasks_Hours_NonNegative",
                table: "Tasks",
                sql: "[EstimatedHours] >= 0 AND [ActualHours] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignees_TaskId_UnitId",
                table: "TaskAssignees",
                columns: new[] { "TaskId", "UnitId" },
                unique: true,
                filter: "[UnitId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaskAssignee_One_Target",
                table: "TaskAssignees",
                sql: "([UserId] IS NOT NULL AND [UnitId] IS NULL) OR ([UserId] IS NULL AND [UnitId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ProgressId",
                table: "Reviews",
                column: "ProgressId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Progress_HoursSpent_NonNegative",
                table: "Progresses",
                sql: "[HoursSpent] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Progress_Percent_Range",
                table: "Progresses",
                sql: "[Percent] >= 0 AND [Percent] <= 100");

            migrationBuilder.CreateIndex(
                name: "IX_CommentSeens_CommentId_UserId",
                table: "CommentSeens",
                columns: new[] { "CommentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentReactions_CommentId_UserId",
                table: "CommentReactions",
                columns: new[] { "CommentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumns_BoardId_StatusKey",
                table: "BoardColumns",
                columns: new[] { "BoardId", "StatusKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Boards_ProjectId",
                table: "Boards",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CreatedBy",
                table: "Projects",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_UnitId_Name",
                table: "Projects",
                columns: new[] { "UnitId", "Name" },
                unique: true,
                filter: "[UnitId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TaskActivities_TaskId",
                table: "TaskActivities",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskActivities_UserId",
                table: "TaskActivities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskReminders_TaskId",
                table: "TaskReminders",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskReminders_UserId",
                table: "TaskReminders",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_BoardColumns_ColumnId",
                table: "Tasks",
                column: "ColumnId",
                principalTable: "BoardColumns",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Boards_BoardId",
                table: "Tasks",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Projects_ProjectId",
                table: "Tasks",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_BoardColumns_ColumnId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Boards_BoardId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Projects_ProjectId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "BoardColumns");

            migrationBuilder.DropTable(
                name: "TaskActivities");

            migrationBuilder.DropTable(
                name: "TaskReminders");

            migrationBuilder.DropTable(
                name: "Boards");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_BoardId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ColumnId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tasks_Hours_NonNegative",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignees_TaskId_UnitId",
                table: "TaskAssignees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaskAssignee_One_Target",
                table: "TaskAssignees");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ProgressId",
                table: "Reviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Progress_HoursSpent_NonNegative",
                table: "Progresses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Progress_Percent_Range",
                table: "Progresses");

            migrationBuilder.DropIndex(
                name: "IX_CommentSeens_CommentId_UserId",
                table: "CommentSeens");

            migrationBuilder.DropIndex(
                name: "IX_CommentReactions_CommentId_UserId",
                table: "CommentReactions");

            migrationBuilder.DropColumn(
                name: "UploadedBy",
                table: "UploadFiles");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ColumnId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "CompletedBy",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ParentTaskId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "RequiresReview",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Tasks");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ProgressId",
                table: "Reviews",
                column: "ProgressId");
        }
    }
}
