using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoCheaper.Trips.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "DriverSnapshots",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "DriverSnapshots");
        }
    }
}
