using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppointMe.Identity.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "identity",
                table: "Users",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                schema: "identity",
                table: "Users");
        }
    }
}
