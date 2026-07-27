using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkManagementSystem.Infrastructure.Data;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260713093000_RemoveTaskEstimatedHoursAndEnsureJoinedUnitAt")]
    public partial class RemoveTaskEstimatedHoursAndEnsureJoinedUnitAt : Migration
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

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_Tasks_Hours_NonNegative'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[Tasks]')
                )
                BEGIN
                    ALTER TABLE [dbo].[Tasks]
                    DROP CONSTRAINT [CK_Tasks_Hours_NonNegative];
                END
            ");

            migrationBuilder.Sql("UPDATE [dbo].[Tasks] SET [ActualHours] = 0 WHERE [ActualHours] < 0;");

            migrationBuilder.Sql(@"
                DECLARE @estimatedHoursDefaultConstraint sysname;
                DECLARE @dropEstimatedHoursDefaultSql nvarchar(max);

                SELECT @estimatedHoursDefaultConstraint = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[Tasks]')
                  AND c.name = N'EstimatedHours';

                IF @estimatedHoursDefaultConstraint IS NOT NULL
                BEGIN
                    SET @dropEstimatedHoursDefaultSql = N'ALTER TABLE [dbo].[Tasks] DROP CONSTRAINT ' + QUOTENAME(@estimatedHoursDefaultConstraint);
                    EXEC sp_executesql @dropEstimatedHoursDefaultSql;
                END
            ");

            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'[dbo].[Tasks]', N'EstimatedHours') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Tasks]
                    DROP COLUMN [EstimatedHours];
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_Tasks_ActualHours_NonNegative'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[Tasks]')
                )
                BEGIN
                    ALTER TABLE [dbo].[Tasks]
                    ADD CONSTRAINT [CK_Tasks_ActualHours_NonNegative]
                    CHECK ([ActualHours] >= 0);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_Tasks_ActualHours_NonNegative'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[Tasks]')
                )
                BEGIN
                    ALTER TABLE [dbo].[Tasks]
                    DROP CONSTRAINT [CK_Tasks_ActualHours_NonNegative];
                END
            ");

            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'[dbo].[Tasks]', N'EstimatedHours') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Tasks]
                    ADD [EstimatedHours] decimal(18,2) NOT NULL
                        CONSTRAINT [DF_Tasks_EstimatedHours] DEFAULT 0;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_Tasks_Hours_NonNegative'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[Tasks]')
                )
                BEGIN
                    ALTER TABLE [dbo].[Tasks]
                    ADD CONSTRAINT [CK_Tasks_Hours_NonNegative]
                    CHECK ([EstimatedHours] >= 0 AND [ActualHours] >= 0);
                END
            ");
        }
    }
}
