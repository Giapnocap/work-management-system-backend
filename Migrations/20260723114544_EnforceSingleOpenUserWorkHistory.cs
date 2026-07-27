using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleOpenUserWorkHistory : Migration
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

                IF EXISTS (
                    SELECT 1
                    FROM [dbo].[UserWorkHistories]
                    WHERE [EffectiveTo] IS NULL
                    GROUP BY [UserId]
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    THROW 51000, 'Cannot enforce one open work history because duplicate open segments exist.', 1;
                END

                IF EXISTS (
                    SELECT 1
                    FROM [dbo].[UserWorkHistories] AS h
                    INNER JOIN [dbo].[Users] AS u ON u.[Id] = h.[UserId]
                    WHERE h.[EffectiveTo] IS NULL
                      AND (
                          ISNULL(CONVERT(nvarchar(36), h.[UnitId]), N'') <>
                              ISNULL(CONVERT(nvarchar(36), u.[UnitId]), N'')
                          OR h.[Role] <> u.[Role]
                      )
                )
                BEGIN
                    THROW 51001, 'Cannot enforce work history integrity because current user state is inconsistent.', 1;
                END
            ");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkHistories_UserId",
                table: "UserWorkHistories",
                column: "UserId",
                unique: true,
                filter: "[EffectiveTo] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserWorkHistories_UserId",
                table: "UserWorkHistories");
        }
    }
}
