using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoCheaper.Trips.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyToTrip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Trips",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Trips");
        }
    }
}
