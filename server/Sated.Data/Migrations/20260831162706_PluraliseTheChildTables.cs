using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sated.Data.Migrations
{
    /// <inheritdoc />
    public partial class PluraliseTheChildTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FoodServing_Foods_FoodId",
                table: "FoodServing");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredient_Foods_FoodId",
                table: "RecipeIngredient");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredient_Recipes_RecipeId",
                table: "RecipeIngredient");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RecipeIngredient",
                table: "RecipeIngredient");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FoodServing",
                table: "FoodServing");

            migrationBuilder.RenameTable(
                name: "RecipeIngredient",
                newName: "RecipeIngredients");

            migrationBuilder.RenameTable(
                name: "FoodServing",
                newName: "FoodServings");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredient_RecipeId",
                table: "RecipeIngredients",
                newName: "IX_RecipeIngredients_RecipeId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredient_FoodId",
                table: "RecipeIngredients",
                newName: "IX_RecipeIngredients_FoodId");

            migrationBuilder.RenameIndex(
                name: "IX_FoodServing_FoodId",
                table: "FoodServings",
                newName: "IX_FoodServings_FoodId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RecipeIngredients",
                table: "RecipeIngredients",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FoodServings",
                table: "FoodServings",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FoodServings_Foods_FoodId",
                table: "FoodServings",
                column: "FoodId",
                principalTable: "Foods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredients_Foods_FoodId",
                table: "RecipeIngredients",
                column: "FoodId",
                principalTable: "Foods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredients_Recipes_RecipeId",
                table: "RecipeIngredients",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FoodServings_Foods_FoodId",
                table: "FoodServings");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredients_Foods_FoodId",
                table: "RecipeIngredients");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredients_Recipes_RecipeId",
                table: "RecipeIngredients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RecipeIngredients",
                table: "RecipeIngredients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FoodServings",
                table: "FoodServings");

            migrationBuilder.RenameTable(
                name: "RecipeIngredients",
                newName: "RecipeIngredient");

            migrationBuilder.RenameTable(
                name: "FoodServings",
                newName: "FoodServing");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredients_RecipeId",
                table: "RecipeIngredient",
                newName: "IX_RecipeIngredient_RecipeId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredients_FoodId",
                table: "RecipeIngredient",
                newName: "IX_RecipeIngredient_FoodId");

            migrationBuilder.RenameIndex(
                name: "IX_FoodServings_FoodId",
                table: "FoodServing",
                newName: "IX_FoodServing_FoodId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RecipeIngredient",
                table: "RecipeIngredient",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FoodServing",
                table: "FoodServing",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FoodServing_Foods_FoodId",
                table: "FoodServing",
                column: "FoodId",
                principalTable: "Foods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredient_Foods_FoodId",
                table: "RecipeIngredient",
                column: "FoodId",
                principalTable: "Foods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredient_Recipes_RecipeId",
                table: "RecipeIngredient",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
