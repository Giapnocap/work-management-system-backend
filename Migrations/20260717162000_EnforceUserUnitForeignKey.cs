using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkManagementSystem.Infrastructure.Data;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260717162000_EnforceUserUnitForeignKey")]
    public partial class EnforceUserUnitForeignKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [dbo].[Users]
                SET [UnitId] = NULL
                WHERE [UnitId] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [dbo].[Units]
                      WHERE [Units].[Id] = [Users].[UnitId]
                        AND [Units].[IsDeleted] = CAST(0 AS bit)
                  );
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Users_UnitId'
                      AND object_id = OBJECT_ID(N'[dbo].[Users]')
                )
                BEGIN
                    CREATE INDEX [IX_Users_UnitId] ON [dbo].[Users] ([UnitId]);
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_Users_Units_UnitId'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[Users]')
                )
                BEGIN
                    ALTER TABLE [dbo].[Users]
                    ADD CONSTRAINT [FK_Users_Units_UnitId]
                    FOREIGN KEY ([UnitId])
                    REFERENCES [dbo].[Units] ([Id])
                    ON DELETE NO ACTION;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_Users_Units_UnitId'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[Users]')
                )
                BEGIN
                    ALTER TABLE [dbo].[Users]
                    DROP CONSTRAINT [FK_Users_Units_UnitId];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Users_UnitId'
                      AND object_id = OBJECT_ID(N'[dbo].[Users]')
                )
                BEGIN
                    DROP INDEX [IX_Users_UnitId] ON [dbo].[Users];
                END
            ");
        }
    }
}
