using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class EnforceTaskWorkflowStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [Tasks]
                SET [Status] = 1,
                    [CompletedAt] = NULL,
                    [CompletedBy] = NULL
                WHERE [Status] = 4;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tasks_Status_Range",
                table: "Tasks",
                sql: "[Status] >= 0 AND [Status] <= 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Tasks_Status_Range",
                table: "Tasks");
        }
    }
}
