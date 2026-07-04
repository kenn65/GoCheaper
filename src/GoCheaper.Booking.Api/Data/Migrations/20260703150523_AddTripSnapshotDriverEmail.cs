using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoCheaper.Booking.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTripSnapshotDriverEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DriverEmail",
                table: "TripSnapshots",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriverEmail",
                table: "TripSnapshots");
        }
    }
}
