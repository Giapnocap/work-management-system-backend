using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUploadTaskIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE uploadFile
                SET TaskId = progress.TaskId
                FROM UploadFiles AS uploadFile
                INNER JOIN Progresses AS progress ON progress.Id = uploadFile.ProgressId
                WHERE uploadFile.TaskId IS NULL;

                IF EXISTS (SELECT 1 FROM UploadFiles WHERE TaskId IS NULL)
                    THROW 51000, 'Cannot enforce upload integrity because unlinked upload files exist.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM UploadFiles AS uploadFile
                    INNER JOIN Progresses AS progress ON progress.Id = uploadFile.ProgressId
                    WHERE uploadFile.TaskId <> progress.TaskId)
                    THROW 51000, 'Cannot enforce upload integrity because task and progress links do not match.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM UploadFiles AS uploadFile
                    LEFT JOIN Tasks AS taskItem ON taskItem.Id = uploadFile.TaskId
                    WHERE taskItem.Id IS NULL)
                    THROW 51000, 'Cannot enforce upload integrity because an upload references a missing task.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM UploadFiles AS uploadFile
                    LEFT JOIN Progresses AS progress ON progress.Id = uploadFile.ProgressId
                    WHERE uploadFile.ProgressId IS NOT NULL AND progress.Id IS NULL)
                    THROW 51000, 'Cannot enforce upload integrity because an upload references a missing progress report.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM UploadFiles AS uploadFile
                    LEFT JOIN Users AS uploader ON uploader.Id = uploadFile.UploadedBy
                    WHERE uploadFile.UploadedBy IS NOT NULL AND uploader.Id IS NULL)
                    THROW 51000, 'Cannot enforce upload integrity because an upload references a missing uploader.', 1;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "TaskId",
                table: "UploadFiles",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Progresses_Id_TaskId",
                table: "Progresses",
                columns: new[] { "Id", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadFiles_ProgressId_TaskId",
                table: "UploadFiles",
                columns: new[] { "ProgressId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadFiles_TaskId",
                table: "UploadFiles",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadFiles_UploadedBy",
                table: "UploadFiles",
                column: "UploadedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_UploadFiles_Progresses_ProgressId_TaskId",
                table: "UploadFiles",
                columns: new[] { "ProgressId", "TaskId" },
                principalTable: "Progresses",
                principalColumns: new[] { "Id", "TaskId" });

            migrationBuilder.AddForeignKey(
                name: "FK_UploadFiles_Tasks_TaskId",
                table: "UploadFiles",
                column: "TaskId",
                principalTable: "Tasks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UploadFiles_Users_UploadedBy",
                table: "UploadFiles",
                column: "UploadedBy",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UploadFiles_Progresses_ProgressId_TaskId",
                table: "UploadFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UploadFiles_Tasks_TaskId",
                table: "UploadFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UploadFiles_Users_UploadedBy",
                table: "UploadFiles");

            migrationBuilder.DropIndex(
                name: "IX_UploadFiles_ProgressId_TaskId",
                table: "UploadFiles");

            migrationBuilder.DropIndex(
                name: "IX_UploadFiles_TaskId",
                table: "UploadFiles");

            migrationBuilder.DropIndex(
                name: "IX_UploadFiles_UploadedBy",
                table: "UploadFiles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Progresses_Id_TaskId",
                table: "Progresses");

            migrationBuilder.AlterColumn<Guid>(
                name: "TaskId",
                table: "UploadFiles",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }
    }
}
