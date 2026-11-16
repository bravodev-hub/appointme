using AppointMe.Booking.Attendees;
using AppointMe.Booking.Database;
using Microsoft.Extensions.Logging;

namespace AppointMe.Booking.Appointments.SeedDemoAppointments;

public sealed partial class SeedDemoAppointmentsCommandHandler(
    BookingDbContext dbContext,
    SeedDemoAppointments seeder,
    ILogger<SeedDemoAppointmentsCommandHandler> logger)
{
    public async Task HandleAsync(SeedDemoAppointmentsCommand command, CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var providerIds = await dbContext.ServiceProviders
            .Select(provider => provider.Id)
            .ToListAsync(cancellationToken);

        var appointments = seeder.Generate(
            companyId, new AttendeeId(command.AttendeeId), providerIds, command.Count);

        dbContext.Appointments.AddRange(appointments);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogDemoAppointmentsScheduled(command.Count, command.AttendeeId, companyId.Value);
    }

    [LoggerMessage(LogLevel.Information,
        "Scheduled {Count} demo appointments for attendee {AttendeeId} in company {CompanyId}.")]
    private partial void LogDemoAppointmentsScheduled(int count, Guid attendeeId, Guid companyId);
}
