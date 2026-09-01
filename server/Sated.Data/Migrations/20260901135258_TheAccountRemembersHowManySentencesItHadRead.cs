using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sated.Data.Migrations
{
    /// <inheritdoc />
    public partial class TheAccountRemembersHowManySentencesItHadRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MealParseWindowStartedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MealParsesUsed",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MealParseWindowStartedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MealParsesUsed",
                table: "AspNetUsers");
        }
    }
}
