using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sated.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNutrientsToFoods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Nutrients_Calcium",
                table: "Foods",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_Calories",
                table: "Foods",
                type: "double precision",
                nullable: false);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_Fat",
                table: "Foods",
                type: "double precision",
                nullable: false);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_Fiber",
                table: "Foods",
                type: "double precision",
                nullable: false);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_Iron",
                table: "Foods",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_Leucine",
                table: "Foods",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_Magnesium",
                table: "Foods",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_Potassium",
                table: "Foods",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_Protein",
                table: "Foods",
                type: "double precision",
                nullable: false);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_SaturatedFat",
                table: "Foods",
                type: "double precision",
                nullable: false);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_Sodium",
                table: "Foods",
                type: "double precision",
                nullable: false);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_Thiamine",
                table: "Foods",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_VitaminA",
                table: "Foods",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_VitaminC",
                table: "Foods",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_VitaminD",
                table: "Foods",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Nutrients_VitaminE",
                table: "Foods",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nutrients_Calcium",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_Calories",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_Fat",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_Fiber",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_Iron",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_Leucine",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_Magnesium",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_Potassium",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_Protein",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_SaturatedFat",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_Sodium",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_Thiamine",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_VitaminA",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_VitaminC",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_VitaminD",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Nutrients_VitaminE",
                table: "Foods");
        }
    }
}
