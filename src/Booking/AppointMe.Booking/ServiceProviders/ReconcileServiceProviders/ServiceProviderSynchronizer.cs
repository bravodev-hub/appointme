using AppointMe.Booking.Database;
using AppointMe.Booking.ServiceProviders.CreateServiceProvider;
using AppointMe.Booking.ServiceProviders.DeleteServiceProvider;
using AppointMe.Booking.ServiceProviders.RestoreServiceProvider;
using AppointMe.Booking.ServiceProviders.UpdateServiceProvider;
using AppointMe.Organizations.Contracts.Employees;

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

        await (ServiceProviderChange.Decide(existing, snapshot) switch
        {
            ProviderCreation creation => CreateServiceProvider(creation.Snapshot, cancellationToken),
            ProviderUpdate update => UpdateServiceProvider(update.Existing, update.Snapshot),
            ProviderDeletion deletion => DeleteServiceProvider(deletion.Existing),
            NoProviderChange => Task.CompletedTask
        });
    }

    private static Task DeleteServiceProvider(ServiceProvider existing)
    {
        existing.Delete();
        return Task.CompletedTask;
    }

    private static Task UpdateServiceProvider(ServiceProvider existing, EmployeeSnapshot snapshot)
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
