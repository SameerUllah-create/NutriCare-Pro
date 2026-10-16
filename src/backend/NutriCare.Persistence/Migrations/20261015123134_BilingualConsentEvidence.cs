using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutriCare.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BilingualConsentEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConsentTextUrdu",
                table: "ConsentRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyTitle",
                table: "ConsentRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresentedLanguage",
                table: "ConsentRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentTextUrdu",
                table: "ConsentRecords");

            migrationBuilder.DropColumn(
                name: "PolicyTitle",
                table: "ConsentRecords");

            migrationBuilder.DropColumn(
                name: "PresentedLanguage",
                table: "ConsentRecords");
        }
    }
}
