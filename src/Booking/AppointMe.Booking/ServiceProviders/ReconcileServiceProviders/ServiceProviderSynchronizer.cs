using AppointMe.Booking.Database;
using AppointMe.Booking.ServiceProviders.CreateServiceProvider;
using AppointMe.Booking.ServiceProviders.DeleteServiceProvider;
using AppointMe.Booking.ServiceProviders.RestoreServiceProvider;
using AppointMe.Booking.ServiceProviders.UpdateServiceProvider;
using AppointMe.Organizations.Contracts.Employees;
using AppointMe.Shared.Authorization.Roles;

namespace AppointMe.Booking.ServiceProviders.ReconcileServiceProviders;

public sealed class ServiceProviderSynchronizer(BookingDbContext dbContext)
{
    public async Task Apply(ServiceProviderId? providerId, EmployeeSnapshot? snapshot,
        CancellationToken cancellationToken)
    {
        ServiceProvider? existing = null;
        if (providerId is { } id)
        {
            existing = await dbContext.ServiceProviders
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(provider => provider.Id == id, cancellationToken);
        }

        var isStaff = snapshot?.Roles.Contains(Role.Staff) == true;

        await ((existing, snapshot, isStaff) switch
        {
            // Doesn't exist, needs to be created
            (existing: null, snapshot: not null, isStaff: true) =>
                CreateServiceProvider(snapshot, cancellationToken),

            // Exists, needs update (and potential restoration)
            (existing: not null, snapshot: not null, isStaff: true) =>
                UpdateServiceProvider(existing, snapshot),

            // Exists but no longer has role, needs deletion
            (existing: not null, snapshot: _, isStaff: false) =>
                DeleteServiceProvider(existing),

            _ => Task.CompletedTask
        });
    }

    private Task DeleteServiceProvider(ServiceProvider existing)
    {
        if (existing.IsDeleted)
        {
            return Task.CompletedTask;
        }

        existing.Delete();
        return Task.CompletedTask;
    }

    private Task UpdateServiceProvider(ServiceProvider existing, EmployeeSnapshot snapshot)
    {
        if (existing.IsDeleted)
        {
            existing.Restore();
        }

        existing.Update(PersonName.Create(snapshot.FirstName, snapshot.LastName));
        return Task.CompletedTask;
    }

    private async Task CreateServiceProvider(EmployeeSnapshot snapshot, CancellationToken cancellationToken)
    {
        var serviceProvider = ServiceProvider.Create(
            id: new ServiceProviderId(snapshot.EmployeeId),
            companyId: snapshot.CompanyId,
            name: PersonName.Create(snapshot.FirstName, snapshot.LastName));
        await dbContext.ServiceProviders.AddAsync(serviceProvider, cancellationToken);
    }
}
