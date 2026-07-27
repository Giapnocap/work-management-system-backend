using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Notifications' AND xtype='U')
                BEGIN
                    CREATE TABLE Notifications (
                        Id uniqueidentifier NOT NULL PRIMARY KEY,
                        UserId uniqueidentifier NOT NULL,
                        Message nvarchar(max) NOT NULL,
                        IsRead bit NOT NULL,
                        CreatedAt datetime2 NOT NULL,
                        CONSTRAINT FK_Notifications_Users_UserId FOREIGN KEY (UserId)
                            REFERENCES Users(Id) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_Notifications_UserId ON Notifications(UserId);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");
        }
    }
}