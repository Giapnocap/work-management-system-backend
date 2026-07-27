using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class EnforceProjectTaskUnitIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SET ANSI_NULLS ON;
                SET ANSI_PADDING ON;
                SET ANSI_WARNINGS ON;
                SET ARITHABORT ON;
                SET CONCAT_NULL_YIELDS_NULL ON;
                SET QUOTED_IDENTIFIER ON;
                SET NUMERIC_ROUNDABORT OFF;

                IF EXISTS (SELECT 1 FROM [dbo].[Projects] WHERE [UnitId] IS NULL)
                    THROW 51010, 'Cannot enforce project scope because a project has no UnitId.', 1;

                IF EXISTS (SELECT 1 FROM [dbo].[Tasks] WHERE [UnitId] IS NULL)
                    THROW 51011, 'Cannot enforce task scope because a task has no UnitId.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [dbo].[Tasks] AS t
                    LEFT JOIN [dbo].[Projects] AS p ON p.[Id] = t.[ProjectId]
                    WHERE t.[ProjectId] IS NOT NULL
                      AND (p.[Id] IS NULL OR p.[UnitId] <> t.[UnitId])
                )
                    THROW 51012, 'Cannot enforce project-task scope because invalid project links exist.', 1;

                UPDATE p
                SET p.[IsArchived] = CAST(0 AS bit)
                FROM [dbo].[Projects] AS p
                WHERE p.[IsArchived] = CAST(1 AS bit)
                  AND EXISTS (
                      SELECT 1
                      FROM [dbo].[Tasks] AS t
                      WHERE t.[ProjectId] = p.[Id]
                        AND t.[IsDeleted] = CAST(0 AS bit)
                        AND t.[Status] <> 3
                  );
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Projects_ProjectId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Projects_UnitId_Name",
                table: "Projects");

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Projects_Id_UnitId",
                table: "Projects",
                columns: new[] { "Id", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_UnitId",
                table: "Tasks",
                columns: new[] { "ProjectId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_UnitId_Name",
                table: "Projects",
                columns: new[] { "UnitId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Projects_ProjectId_UnitId",
                table: "Tasks",
                columns: new[] { "ProjectId", "UnitId" },
                principalTable: "Projects",
                principalColumns: new[] { "Id", "UnitId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Projects_ProjectId_UnitId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_UnitId",
                table: "Tasks");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Projects_Id_UnitId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_UnitId_Name",
                table: "Projects");

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_UnitId_Name",
                table: "Projects",
                columns: new[] { "UnitId", "Name" },
                unique: true,
                filter: "[UnitId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Projects_ProjectId",
                table: "Tasks",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }
    }
}
