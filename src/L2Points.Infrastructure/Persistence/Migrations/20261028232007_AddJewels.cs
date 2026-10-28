using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L2Points.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJewels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JewelTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JewelTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserJewels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    JewelTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserJewels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserJewels_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserJewels_JewelTypes_JewelTypeId",
                        column: x => x.JewelTypeId,
                        principalTable: "JewelTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JewelTypes_Code",
                table: "JewelTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserJewels_JewelTypeId",
                table: "UserJewels",
                column: "JewelTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserJewels_UserId",
                table: "UserJewels",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserJewels");

            migrationBuilder.DropTable(
                name: "JewelTypes");
        }
    }
}
