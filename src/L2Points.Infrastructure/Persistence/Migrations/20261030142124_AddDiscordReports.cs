using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L2Points.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscordReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiscordUserId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DiscordUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GuildId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ChannelId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StateJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscordReports_CreatedAtUtc",
                table: "DiscordReports",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscordReports");
        }
    }
}
