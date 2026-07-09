using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoCheaper.Identity.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsProfileComplete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProfileComplete",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing users registered before this flow had a complete profile
            migrationBuilder.Sql("UPDATE Users SET IsProfileComplete = 1 WHERE FirstName <> '' AND LastName <> '' AND (IsDriver = 1 OR IsPassenger = 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsProfileComplete",
                table: "Users");
        }
    }
}
