using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkManagementSystem.Infrastructure.Data;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715090000_AddKpiDataIntegrityConstraints")]
    public partial class AddKpiDataIntegrityConstraints : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [dbo].[KpiPeriods]
                SET [EndDate] = DATEADD(DAY, 1, [StartDate])
                WHERE [EndDate] <= [StartDate];
            ");

            migrationBuilder.Sql(@"
                UPDATE [dbo].[KpiResults]
                SET
                    [Score] = CASE WHEN [Score] < 0 THEN 0 ELSE [Score] END,
                    [TotalTasks] = CASE WHEN [TotalTasks] < 0 THEN 0 ELSE [TotalTasks] END,
                    [CompletedOnTime] = CASE WHEN [CompletedOnTime] < 0 THEN 0 ELSE [CompletedOnTime] END,
                    [CompletedLate] = CASE WHEN [CompletedLate] < 0 THEN 0 ELSE [CompletedLate] END,
                    [OverdueTasks] = CASE WHEN [OverdueTasks] < 0 THEN 0 ELSE [OverdueTasks] END,
                    [RejectedReports] = CASE WHEN [RejectedReports] < 0 THEN 0 ELSE [RejectedReports] END,
                    [BonusPoints] = CASE WHEN [BonusPoints] < 0 THEN 0 ELSE [BonusPoints] END,
                    [PenaltyPoints] = CASE WHEN [PenaltyPoints] < 0 THEN 0 ELSE [PenaltyPoints] END,
                    [ReviewPenaltyPoints] = CASE WHEN [ReviewPenaltyPoints] < 0 THEN 0 ELSE [ReviewPenaltyPoints] END,
                    [UnitAverageScore] = CASE WHEN [UnitAverageScore] < 0 THEN 0 ELSE [UnitAverageScore] END,
                    [PersonalScore] = CASE WHEN [PersonalScore] < 0 THEN 0 ELSE [PersonalScore] END,
                    [EffectiveTo] = CASE WHEN [EffectiveTo] < [EffectiveFrom] THEN [EffectiveFrom] ELSE [EffectiveTo] END;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_KpiPeriods_Date_Range'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[KpiPeriods]')
                )
                BEGIN
                    ALTER TABLE [dbo].[KpiPeriods]
                    ADD CONSTRAINT [CK_KpiPeriods_Date_Range]
                    CHECK ([EndDate] > [StartDate]);
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_KpiResults_Effective_Range'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[KpiResults]')
                )
                BEGIN
                    ALTER TABLE [dbo].[KpiResults]
                    ADD CONSTRAINT [CK_KpiResults_Effective_Range]
                    CHECK ([EffectiveTo] >= [EffectiveFrom]);
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_KpiResults_NonNegative'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[KpiResults]')
                )
                BEGIN
                    ALTER TABLE [dbo].[KpiResults]
                    ADD CONSTRAINT [CK_KpiResults_NonNegative]
                    CHECK (
                        [Score] >= 0
                        AND [TotalTasks] >= 0
                        AND [CompletedOnTime] >= 0
                        AND [CompletedLate] >= 0
                        AND [OverdueTasks] >= 0
                        AND [RejectedReports] >= 0
                        AND [BonusPoints] >= 0
                        AND [PenaltyPoints] >= 0
                        AND [ReviewPenaltyPoints] >= 0
                        AND [UnitAverageScore] >= 0
                        AND [PersonalScore] >= 0
                    );
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_KpiResults_NonNegative'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[KpiResults]')
                )
                BEGIN
                    ALTER TABLE [dbo].[KpiResults]
                    DROP CONSTRAINT [CK_KpiResults_NonNegative];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_KpiResults_Effective_Range'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[KpiResults]')
                )
                BEGIN
                    ALTER TABLE [dbo].[KpiResults]
                    DROP CONSTRAINT [CK_KpiResults_Effective_Range];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_KpiPeriods_Date_Range'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[KpiPeriods]')
                )
                BEGIN
                    ALTER TABLE [dbo].[KpiPeriods]
                    DROP CONSTRAINT [CK_KpiPeriods_Date_Range];
                END
            ");
        }
    }
}
