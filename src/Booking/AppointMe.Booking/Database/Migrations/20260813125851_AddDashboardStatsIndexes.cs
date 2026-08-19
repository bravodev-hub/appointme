using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppointMe.Booking.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardStatsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Raw SQL, not fluent HasIndex: this composite index spans Appointments.CompanyId
            // (a plain scalar on the owner) and Appointments.Start (a property of the owned
            // Period value object, mapped via OwnsOne in AppointmentTypeConfiguration). EF's
            // fluent HasIndex API cannot express an index across an owner property and an
            // owned-type property together, so the index is created directly in SQL instead.
            migrationBuilder.Sql(
                """
                CREATE NONCLUSTERED INDEX [IX_Appointments_CompanyId_Start]
                    ON [booking].[Appointments] ([CompanyId], [Start])
                    INCLUDE ([End], [Status], [ProviderId], [AttendeeId]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX [IX_Appointments_CompanyId_Start] ON [booking].[Appointments];");
        }
    }
}
