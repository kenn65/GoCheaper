using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoCheaper.Booking.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPassengerEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PassengerEmail",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PassengerEmail",
                table: "Bookings");
        }
    }
}
