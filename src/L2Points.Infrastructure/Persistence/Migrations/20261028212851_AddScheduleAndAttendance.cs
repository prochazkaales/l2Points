using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L2Points.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleAndAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ScheduleType = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    ExpectedPerWeek = table.Column<int>(type: "int", nullable: true),
                    WeeklyDay = table.Column<int>(type: "int", nullable: true),
                    BiweeklyAnchorDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TimeUtc = table.Column<TimeOnly>(type: "time", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ActivitySlotPoints",
                columns: table => new
                {
                    ActivityId = table.Column<int>(type: "int", nullable: false),
                    Slot = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySlotPoints", x => new { x.ActivityId, x.Slot });
                    table.ForeignKey(
                        name: "FK_ActivitySlotPoints_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivitySpawns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    Slot = table.Column<int>(type: "int", nullable: false),
                    TimeUtc = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySpawns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivitySpawns_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActivityId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Slot = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendances_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Code",
                table: "Activities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySpawns_ActivityId_DayOfWeek_Slot",
                table: "ActivitySpawns",
                columns: new[] { "ActivityId", "DayOfWeek", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_ActivityId",
                table: "Attendances",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_Date",
                table: "Attendances",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_UserId_ActivityId_Date_Slot",
                table: "Attendances",
                columns: new[] { "UserId", "ActivityId", "Date", "Slot" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivitySlotPoints");

            migrationBuilder.DropTable(
                name: "ActivitySpawns");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "Activities");
        }
    }
}
