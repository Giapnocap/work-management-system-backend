using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class StoreUploadStorageKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "UploadFiles",
                newName: "StorageKey");

            migrationBuilder.Sql(
                """
                UPDATE [UploadFiles]
                SET [StorageKey] =
                    CASE
                        WHEN CHARINDEX('/', REVERSE(REPLACE([StorageKey], '\', '/'))) > 0
                            THEN RIGHT(
                                REPLACE([StorageKey], '\', '/'),
                                CHARINDEX('/', REVERSE(REPLACE([StorageKey], '\', '/'))) - 1)
                        ELSE [StorageKey]
                    END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [UploadFiles]
                SET [FileName] = LEFT([FileName], 200)
                WHERE LEN([FileName]) > 200;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "UploadFiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "StorageKey",
                table: "UploadFiles",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StorageKey",
                table: "UploadFiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.RenameColumn(
                name: "StorageKey",
                table: "UploadFiles",
                newName: "FilePath");

            migrationBuilder.Sql(
                """
                UPDATE [UploadFiles]
                SET [FilePath] = CONCAT('Uploads/', [FilePath]);
                """);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "UploadFiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

        }
    }
}
