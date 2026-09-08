using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    public partial class AddTaskIdToUploadFile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'[dbo].[UploadFiles]', N'TaskId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[UploadFiles]
                    ADD [TaskId] uniqueidentifier NULL;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'[dbo].[UploadFiles]', N'TaskId') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[UploadFiles]
                    DROP COLUMN [TaskId];
                END
            ");
        }
    }
}
