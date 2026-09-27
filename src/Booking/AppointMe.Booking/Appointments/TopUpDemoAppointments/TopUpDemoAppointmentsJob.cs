using AppointMe.Booking.Database;

namespace AppointMe.Booking.Appointments.TopUpDemoAppointments;

public sealed class TopUpDemoAppointmentsJob(BookingDbContext dbContext, IMessageBus bus)
{
    // The seeder scatters each daily batch over a -21..+21 day window, so the current
    // week's chair utilization scales roughly linearly with this: 40 gave ~215% for a
    // single provider, 15 lands around 80%.
    private const int AppointmentsPerCompany = 15;

    public async Task Run(CancellationToken cancellationToken)
    {
        var companies = await dbContext.BookingCompanies.ToListAsync(cancellationToken);
        foreach (var company in companies)
        {
            await bus.InvokeForCompany(company.Id,
                new TopUpDemoAppointmentsCommand(Count: AppointmentsPerCompany), cancellationToken);
        }
    }
}
