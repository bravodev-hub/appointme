using AppointMe.Booking.Database;

namespace AppointMe.Booking.Appointments.TopUpDemoAppointments;

public sealed class TopUpDemoAppointmentsJob(BookingDbContext dbContext, IMessageBus bus)
{
    // Busy salon: the seeder scatters each daily batch over its window, so in steady
    // state this is also roughly the number of appointments any given day carries.
    private const int AppointmentsPerCompany = 40;

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
