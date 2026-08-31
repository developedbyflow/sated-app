using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sated.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileAndConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveLensId",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WeightKg",
                table: "AspNetUsers",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConsentDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Consents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    GivenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Consents_ConsentDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "ConsentDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ConsentDocuments",
                columns: new[] { "Id", "PublishedAt", "Purpose", "Text", "Version" },
                values: new object[] { 1, new DateTimeOffset(new DateTime(2026, 8, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "HealthData", "Sated needs two kinds of information about you that count as health data: your body\nweight, and what you eat.\n\nYour weight is used to work out your daily protein target. What you eat is used to\ngrade your food and to show you your day. Neither is used for anything else, and\nneither is shared with anyone outside Sated.\n\nThe law treats this as a special category of personal data. That is why you are being\nasked here, separately from the terms you accepted when you created your account.\nNothing on this screen covers marketing, analytics, or passing anything to anyone else.\nSated does none of those.\n\nYou can withdraw this at any time from Settings, in one action — the same as giving it.\n\nWithdrawing deletes the data it covers: your weight, and everything you have logged.\nYour account stays and you can still sign in, but Sated has nothing left to work with,\nso grades and targets stop. That is not a penalty for withdrawing; it is what the\nproduct is made of.", "2026-08-31" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentDocuments_Purpose_Version",
                table: "ConsentDocuments",
                columns: new[] { "Purpose", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consents_DocumentId",
                table: "Consents",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Consents_UserId",
                table: "Consents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Consents");

            migrationBuilder.DropTable(
                name: "ConsentDocuments");

            migrationBuilder.DropColumn(
                name: "ActiveLensId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "AspNetUsers");
        }
    }
}
