using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sated.Data.Migrations
{
    /// <inheritdoc />
    public partial class MealEntryRemembersItsRecipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromRecipeId",
                table: "MealEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromRecipeName",
                table: "MealEntries",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromRecipeId",
                table: "MealEntries");

            migrationBuilder.DropColumn(
                name: "FromRecipeName",
                table: "MealEntries");
        }
    }
}
