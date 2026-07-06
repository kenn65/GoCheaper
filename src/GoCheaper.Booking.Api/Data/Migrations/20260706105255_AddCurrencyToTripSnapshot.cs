using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoCheaper.Booking.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyToTripSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "TripSnapshots",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "TripSnapshots");
        }
    }
}
