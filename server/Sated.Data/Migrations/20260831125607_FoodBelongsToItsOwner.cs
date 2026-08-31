using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sated.Data.Migrations
{
    /// <inheritdoc />
    public partial class FoodBelongsToItsOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Foods",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Foods_OwnerId",
                table: "Foods",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Foods_AspNetUsers_OwnerId",
                table: "Foods",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Foods_AspNetUsers_OwnerId",
                table: "Foods");

            migrationBuilder.DropIndex(
                name: "IX_Foods_OwnerId",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Foods");
        }
    }
}
