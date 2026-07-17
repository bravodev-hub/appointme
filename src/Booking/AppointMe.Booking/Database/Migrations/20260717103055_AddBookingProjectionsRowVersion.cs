using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppointMe.Booking.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingProjectionsRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "booking",
                table: "ServiceProviders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "booking",
                table: "BookingCompanies",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "booking",
                table: "Attendees",
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
                schema: "booking",
                table: "ServiceProviders");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "booking",
                table: "BookingCompanies");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "booking",
                table: "Attendees");
        }
    }
}
