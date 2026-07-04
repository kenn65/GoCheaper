using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoCheaper.Booking.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverFullNameToTripSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DriverFullName",
                table: "TripSnapshots",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriverFullName",
                table: "TripSnapshots");
        }
    }
}
