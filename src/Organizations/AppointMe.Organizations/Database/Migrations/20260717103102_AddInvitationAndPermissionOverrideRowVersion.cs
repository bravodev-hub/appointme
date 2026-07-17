using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppointMe.Organizations.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationAndPermissionOverrideRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "organizations",
                table: "RolePermissionOverrides",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "organizations",
                table: "EmployeeInvitations",
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
                table: "RolePermissionOverrides");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "organizations",
                table: "EmployeeInvitations");
        }
    }
}
