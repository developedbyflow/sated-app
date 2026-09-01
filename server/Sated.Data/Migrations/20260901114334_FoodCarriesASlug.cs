using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sated.Data.Migrations
{
    /// <inheritdoc />
    public partial class FoodCarriesASlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Foods",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Foods"
                SET "Slug" = trim(both '-' from
                    regexp_replace(lower("Description"), '[^a-z0-9]+', '-', 'g'))
                WHERE "OwnerId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Foods_Slug",
                table: "Foods",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Foods_Slug",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Foods");
        }
    }
}
