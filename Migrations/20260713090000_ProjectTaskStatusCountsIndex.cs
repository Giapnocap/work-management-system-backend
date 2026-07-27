using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkManagementSystem.Infrastructure.Data;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260713090000_ProjectTaskStatusCountsIndex")]
    public partial class ProjectTaskStatusCountsIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Tasks_ProjectId_Status'
                      AND object_id = OBJECT_ID(N'[dbo].[Tasks]')
                )
                BEGIN
                    CREATE INDEX [IX_Tasks_ProjectId_Status]
                    ON [dbo].[Tasks] ([ProjectId], [Status]);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Tasks_ProjectId_Status'
                      AND object_id = OBJECT_ID(N'[dbo].[Tasks]')
                )
                BEGIN
                    DROP INDEX [IX_Tasks_ProjectId_Status] ON [dbo].[Tasks];
                END
            ");
        }
    }
}
