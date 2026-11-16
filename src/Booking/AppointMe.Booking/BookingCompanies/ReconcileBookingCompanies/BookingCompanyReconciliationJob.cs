using Microsoft.Extensions.Logging;

namespace AppointMe.Booking.BookingCompanies.ReconcileBookingCompanies;

public sealed partial class BookingCompanyReconciliationJob(
    IMessageBus bus,
    ILogger<BookingCompanyReconciliationJob> logger)
{
    public async Task Run(CancellationToken cancellationToken)
    {
        await bus.InvokeAsync(new ReconcileBookingCompaniesCommand(), cancellationToken);
        LogReconciliationScheduled();
    }

    [LoggerMessage(LogLevel.Information, "Booking company reconciliation scheduled.")]
    private partial void LogReconciliationScheduled();
}
