using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L2Points.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestoreActiveFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ActiveFrom",
                table: "AspNetUsers",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Existing players get the default rule back: Monday of the week their account was created
            // (1900-01-01 was a Monday, so the day difference modulo 7 is the offset from Monday).
            migrationBuilder.Sql(
                """
                UPDATE AspNetUsers
                SET ActiveFrom = DATEADD(day, -(DATEDIFF(day, '19000101', CAST(CreatedAt AS date)) % 7), CAST(CreatedAt AS date));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveFrom",
                table: "AspNetUsers");
        }
    }
}
