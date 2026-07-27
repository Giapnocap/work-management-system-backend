using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimisticConcurrencyAndMembershipIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'[dbo].[Tasks]', N'StartDate') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Tasks] ADD [StartDate] datetime2 NULL;
                END;

                IF COL_LENGTH(N'[dbo].[Tasks]', N'DueDate') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Tasks] ADD [DueDate] datetime2 NULL;
                END;
            ");

            migrationBuilder.DropIndex(
                name: "IX_UserUnits_UserId_UnitId",
                table: "UserUnits");

            migrationBuilder.Sql(@"
                DELETE uu
                FROM [dbo].[UserUnits] AS uu
                INNER JOIN [dbo].[Users] AS u ON u.[Id] = uu.[UserId]
                WHERE u.[UnitId] IS NULL OR uu.[UnitId] <> u.[UnitId];
            ");

            migrationBuilder.Sql(@"
                UPDATE [dbo].[Tasks]
                SET [DueDate] = [StartDate]
                WHERE [StartDate] IS NOT NULL
                  AND [DueDate] IS NOT NULL
                  AND [DueDate] < [StartDate];
            ");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Users",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Units",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Tasks",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Projects",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Progresses",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "KpiPeriods",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_UserUnits_UserId",
                table: "UserUnits",
                column: "UserId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tasks_Date_Range",
                table: "Tasks",
                sql: "[StartDate] IS NULL OR [DueDate] IS NULL OR [DueDate] >= [StartDate]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserUnits_UserId",
                table: "UserUnits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tasks_Date_Range",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Progresses");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "KpiPeriods");

            migrationBuilder.CreateIndex(
                name: "IX_UserUnits_UserId_UnitId",
                table: "UserUnits",
                columns: new[] { "UserId", "UnitId" },
                unique: true);
        }
    }
}
