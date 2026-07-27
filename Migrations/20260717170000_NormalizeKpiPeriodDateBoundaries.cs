using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkManagementSystem.Infrastructure.Data;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260717170000_NormalizeKpiPeriodDateBoundaries")]
    public partial class NormalizeKpiPeriodDateBoundaries : Migration
    {
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

                UPDATE [dbo].[KpiPeriods]
                SET
                    [StartDate] = CONVERT(datetime2, CONVERT(date, [StartDate])),
                    [EndDate] = DATEADD(NANOSECOND, -100, DATEADD(DAY, 1, CONVERT(datetime2, CONVERT(date, [EndDate]))))
                WHERE [StartDate] <> CONVERT(datetime2, CONVERT(date, [StartDate]))
                   OR [EndDate] <> DATEADD(NANOSECOND, -100, DATEADD(DAY, 1, CONVERT(datetime2, CONVERT(date, [EndDate]))));
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
