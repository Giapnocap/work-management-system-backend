using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkManagementSystem.Migrations
{
    public partial class AddWorkloadCapacityManagement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PlannedEffortHours",
                table: "Tasks",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserCapacities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeeklyCapacityHours = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCapacities", x => x.Id);
                    table.CheckConstraint("CK_UserCapacities_Effective_Range", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
                    table.CheckConstraint("CK_UserCapacities_WeeklyHours_Positive", "[WeeklyCapacityHours] > 0");
                    table.ForeignKey(
                        name: "FK_UserCapacities_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserCapacities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tasks_PlannedEffortHours_Positive",
                table: "Tasks",
                sql: "[PlannedEffortHours] IS NULL OR [PlannedEffortHours] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserCapacities_ChangedByUserId",
                table: "UserCapacities",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCapacities_UserId",
                table: "UserCapacities",
                column: "UserId",
                unique: true,
                filter: "[EffectiveTo] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserCapacities_UserId_EffectiveFrom",
                table: "UserCapacities",
                columns: new[] { "UserId", "EffectiveFrom" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCapacities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tasks_PlannedEffortHours_Positive",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "PlannedEffortHours",
                table: "Tasks");
        }
    }
}
