using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppointMe.Booking.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "booking",
                table: "Appointments",
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
                table: "Appointments");
        }
    }
}
