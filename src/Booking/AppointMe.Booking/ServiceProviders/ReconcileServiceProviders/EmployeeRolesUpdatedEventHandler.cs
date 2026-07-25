using AppointMe.Booking.Database;
using AppointMe.Organizations.Contracts.Employees;
using AppointMe.Organizations.Contracts.Employees.Events;

namespace AppointMe.Booking.ServiceProviders.ReconcileServiceProviders;

public class EmployeeRolesUpdatedEventHandler(BookingDbContext dbContext, ServiceProviderSynchronizer synchronizer)
{
    public async Task Handle(EmployeeRolesUpdated @event, CancellationToken cancellationToken)
    {
        var snapshot = new EmployeeSnapshot(
            EmployeeId: @event.EmployeeId,
            CompanyId: new CompanyId(@event.CompanyId),
            FirstName: @event.FirstName,
            LastName: @event.LastName,
            Roles: @event.Roles);

        await synchronizer.Apply(new ServiceProviderId(snapshot.EmployeeId), snapshot, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
