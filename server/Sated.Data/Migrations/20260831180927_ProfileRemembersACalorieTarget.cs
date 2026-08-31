using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sated.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProfileRemembersACalorieTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CalorieTargetKcal",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalorieTargetKcal",
                table: "AspNetUsers");
        }
    }
}
