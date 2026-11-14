using AppointMe.Booking.Database;
using AppointMe.Organizations.Contracts.Employees;
using Microsoft.Extensions.Logging;

namespace AppointMe.Booking.ServiceProviders.ReconcileServiceProviders;

public class ReconcileServiceProvidersCommandHandler(
    BookingDbContext dbContext,
    IEmployeeRehydrationSource employeeSource,
    ServiceProviderSynchronizer synchronizer,
    ILogger<ReconcileServiceProvidersCommandHandler> logger
)
{
    public async Task HandleAsync(ReconcileServiceProvidersCommand _, CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var snapshots = await employeeSource.GetAllByCompany(companyId, cancellationToken);

        var locals = await dbContext.ServiceProviders
            .IgnoreQueryFilters()
            .Where(provider => provider.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        var pairs = locals.FullJoin(snapshots,
            local => local.Id.Value,
            snapshot => snapshot.EmployeeId);

        foreach (var (existing, snapshot) in pairs)
        {
            try
            {
                await synchronizer.Apply(existing?.Id, snapshot, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Failed to reconcile service provider {ProviderId} in company {CompanyId}.",
                    existing?.Id.Value ?? snapshot?.EmployeeId, companyId);
                dbContext.ChangeTracker.Clear();
            }
        }
    }
}
