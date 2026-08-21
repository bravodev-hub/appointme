using AppointMe.Booking.Database;

namespace AppointMe.Booking.Appointments.TopUpDemoAppointments;

public sealed class TopUpDemoAppointmentsCommandHandler(
    BookingDbContext dbContext,
    SeedDemoAppointments.SeedDemoAppointments seeder)
{
    public async Task HandleAsync(TopUpDemoAppointmentsCommand command, CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var providerIds = await dbContext.ServiceProviders
            .Select(provider => provider.Id)
            .ToListAsync(cancellationToken);

        var attendeeIds = await dbContext.Attendees
            .Select(attendee => attendee.Id)
            .ToListAsync(cancellationToken);

        if (providerIds.Count == 0 || attendeeIds.Count == 0)
        {
            return;
        }

        var appointments = Enumerable.Range(0, command.Count)
            .Select(_ => attendeeIds[Random.Shared.Next(attendeeIds.Count)])
            .SelectMany(attendeeId => seeder.Generate(companyId, attendeeId, providerIds, count: 1))
            .ToList();

        dbContext.Appointments.AddRange(appointments);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
