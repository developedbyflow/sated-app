using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sated.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConsentNamesTheHeightItCovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ConsentDocuments",
                keyColumn: "Id",
                keyValue: 1,
                column: "Text",
                value: "Sated needs three kinds of information about you that count as health data: your body\nweight, your height, and what you eat.\n\nYour weight and height are used together to work out your daily protein target. What\nyou eat is used to grade your food and to show you your day. None of it is used for\nanything else, and none of it is shared with anyone outside Sated.\n\nThe law treats this as a special category of personal data. That is why you are being\nasked here, separately from the terms you accepted when you created your account.\nNothing on this screen covers marketing, analytics, or passing anything to anyone else.\nSated does none of those.\n\nYou can withdraw this at any time from Settings, in one action — the same as giving it.\n\nWithdrawing deletes the data it covers: your weight, your height, and everything you\nhave logged.\nYour account stays and you can still sign in, but Sated has nothing left to work with,\nso grades and targets stop. That is not a penalty for withdrawing; it is what the\nproduct is made of.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ConsentDocuments",
                keyColumn: "Id",
                keyValue: 1,
                column: "Text",
                value: "Sated needs two kinds of information about you that count as health data: your body\nweight, and what you eat.\n\nYour weight is used to work out your daily protein target. What you eat is used to\ngrade your food and to show you your day. Neither is used for anything else, and\nneither is shared with anyone outside Sated.\n\nThe law treats this as a special category of personal data. That is why you are being\nasked here, separately from the terms you accepted when you created your account.\nNothing on this screen covers marketing, analytics, or passing anything to anyone else.\nSated does none of those.\n\nYou can withdraw this at any time from Settings, in one action — the same as giving it.\n\nWithdrawing deletes the data it covers: your weight, and everything you have logged.\nYour account stays and you can still sign in, but Sated has nothing left to work with,\nso grades and targets stop. That is not a penalty for withdrawing; it is what the\nproduct is made of.");
        }
    }
}
