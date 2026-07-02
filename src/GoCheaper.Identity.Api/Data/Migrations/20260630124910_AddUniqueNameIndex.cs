using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoCheaper.Identity.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_FirstName_LastName",
                table: "Users",
                columns: new[] { "FirstName", "LastName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_FirstName_LastName",
                table: "Users");
        }
    }
}
