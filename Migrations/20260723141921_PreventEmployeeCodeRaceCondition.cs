using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class PreventEmployeeCodeRaceCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "EmployeeCodeSequence");

            migrationBuilder.Sql(
                """
                DECLARE @nextEmployeeCode bigint;

                SELECT @nextEmployeeCode =
                    ISNULL(
                        MAX(
                            TRY_CONVERT(
                                bigint,
                                SUBSTRING([EmployeeCode], 4, LEN([EmployeeCode]) - 3))),
                        0) + 1
                FROM [Users]
                WHERE [EmployeeCode] LIKE N'EMP%'
                  AND LEN([EmployeeCode]) > 3;

                DECLARE @restartSql nvarchar(200) =
                    N'ALTER SEQUENCE [EmployeeCodeSequence] RESTART WITH '
                    + CONVERT(nvarchar(20), @nextEmployeeCode);

                EXEC sys.sp_executesql @restartSql;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "EmployeeCodeSequence");
        }
    }
}
