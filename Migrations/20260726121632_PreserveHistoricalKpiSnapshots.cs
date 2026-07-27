using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class PreserveHistoricalKpiSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeCodeSnapshot",
                table: "KpiResults",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullNameSnapshot",
                table: "KpiResults",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UnitNameSnapshot",
                table: "KpiResults",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE result
                SET
                    result.FullNameSnapshot =
                        COALESCE(NULLIF(LTRIM(RTRIM(employee.FullName)), ''), '-'),
                    result.EmployeeCodeSnapshot =
                        COALESCE(NULLIF(LTRIM(RTRIM(employee.EmployeeCode)), ''), '-'),
                    result.UnitNameSnapshot =
                        COALESCE(NULLIF(LTRIM(RTRIM(unit.Name)), ''), '')
                FROM KpiResults AS result
                INNER JOIN Users AS employee ON employee.Id = result.UserId
                LEFT JOIN Units AS unit ON unit.Id = result.UnitId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeCodeSnapshot",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "FullNameSnapshot",
                table: "KpiResults");

            migrationBuilder.DropColumn(
                name: "UnitNameSnapshot",
                table: "KpiResults");
        }
    }
}
