using AppointMe.Booking.Database;
using AppointMe.Organizations.Contracts.Companies;
using Microsoft.Extensions.Logging;

namespace AppointMe.Booking.BookingCompanies.ReconcileBookingCompanies;

public partial class ReconcileBookingCompaniesCommandHandler(
    BookingDbContext dbContext,
    ICompanyRehydrationSource source,
    BookingCompanySynchronizer synchronizer,
    ILogger<ReconcileBookingCompaniesCommandHandler> logger)
{
    public async Task HandleAsync(ReconcileBookingCompaniesCommand command, CancellationToken cancellationToken)
    {
        var snapshots = await source.GetAll(cancellationToken);

        var processed = 0;
        foreach (var snapshot in snapshots)
        {
            try
            {
                await synchronizer.Apply(snapshot, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                processed++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogCompanyReconciliationFailed(exception, snapshot.CompanyId);
                dbContext.ChangeTracker.Clear();
            }
        }

        LogCompaniesReconciled(processed);
    }

    [LoggerMessage(LogLevel.Error, "Failed to reconcile booking company {CompanyId}.")]
    private partial void LogCompanyReconciliationFailed(Exception exception, Guid companyId);

    [LoggerMessage(LogLevel.Information, "Reconciled {Count} booking companies.")]
    private partial void LogCompaniesReconciled(int count);
}
