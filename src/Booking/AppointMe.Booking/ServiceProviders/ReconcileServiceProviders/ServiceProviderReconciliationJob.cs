using AppointMe.Booking.Database;

namespace AppointMe.Booking.ServiceProviders.ReconcileServiceProviders;

public sealed class ServiceProviderReconciliationJob(BookingDbContext dbContext, IMessageBus bus)
{
    public async Task Run(CancellationToken cancellationToken)
    {
        var companies = await dbContext.BookingCompanies.ToListAsync(cancellationToken);
        foreach (var company in companies)
        {
            await bus.InvokeForCompany(company.Id, new ReconcileServiceProvidersCommand(), cancellationToken);
        }
    }
}
