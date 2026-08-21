using AppointMe.Booking.Database;

namespace AppointMe.Booking.Attendees.ReconcileAttendees;

public sealed class AttendeeReconciliationJob(BookingDbContext dbContext, IMessageBus bus)
{
    public async Task Run(CancellationToken cancellationToken)
    {
        var companies = await dbContext.BookingCompanies.ToListAsync(cancellationToken);
        foreach (var company in companies)
        {
            await bus.InvokeForCompany(company.Id, new ReconcileAttendeesCommand(), cancellationToken);
        }
    }
}
