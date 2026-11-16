using AppointMe.Booking.Database;
using AppointMe.Organizations.Contracts.Employees;
using Microsoft.Extensions.Logging;

namespace AppointMe.Booking.ServiceProviders.ReconcileServiceProviders;

public partial class ReconcileServiceProvidersCommandHandler(
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
                LogProviderReconciliationFailed(exception, existing?.Id.Value ?? snapshot?.EmployeeId, companyId);
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    [LoggerMessage(LogLevel.Error, "Failed to reconcile service provider {ProviderId} in company {CompanyId}.")]
    private partial void LogProviderReconciliationFailed(Exception exception, Guid? providerId, CompanyId companyId);
}
