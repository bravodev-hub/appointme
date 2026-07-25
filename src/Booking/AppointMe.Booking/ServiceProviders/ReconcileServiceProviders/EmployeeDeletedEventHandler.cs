using AppointMe.Booking.Database;
using AppointMe.Organizations.Contracts.Employees.Events;

namespace AppointMe.Booking.ServiceProviders.ReconcileServiceProviders;

public class EmployeeDeletedEventHandler(BookingDbContext dbContext, ServiceProviderSynchronizer synchronizer)
{
    public async Task Handle(EmployeeDeleted @event, CancellationToken cancellationToken)
    {
        var providerId = new ServiceProviderId(@event.EmployeeId);

        await synchronizer.Apply(providerId, snapshot: null, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
