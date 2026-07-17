using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppointMe.Organizations.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeAndCompanyRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "organizations",
                table: "Employees",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "organizations",
                table: "Companies",
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
                schema: "organizations",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "organizations",
                table: "Companies");
        }
    }
}
