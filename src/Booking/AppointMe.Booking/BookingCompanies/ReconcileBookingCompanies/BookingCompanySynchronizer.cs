using AppointMe.Booking.Database;
using AppointMe.Organizations.Contracts.Companies;

namespace AppointMe.Booking.BookingCompanies.ReconcileBookingCompanies;

public sealed class BookingCompanySynchronizer(BookingDbContext dbContext)
{
    public async Task Apply(CompanySnapshot snapshot, CancellationToken cancellationToken)
    {
        await dbContext.BookingCompanies.UpsertAsync(
            id: new CompanyId(snapshot.CompanyId),
            name: snapshot.Name,
            timeZone: TimeZoneInfo.Create(snapshot.TimeZone),
            cancellationToken: cancellationToken);
    }
}
