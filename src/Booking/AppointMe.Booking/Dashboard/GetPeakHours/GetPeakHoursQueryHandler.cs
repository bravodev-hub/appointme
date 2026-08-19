using AppointMe.Booking.Appointments;
using AppointMe.Booking.Dashboard.Database;

namespace AppointMe.Booking.Dashboard.GetPeakHours;

public sealed class GetPeakHoursQueryHandler(DashboardRepository repository)
{
    public async Task<GetPeakHoursResponse> HandleAsync(GetPeakHoursQuery query,
        CompanyId companyId, IPrincipal principal, CancellationToken cancellationToken)
    {
        principal.Require(AppointmentPermissions.ViewStatistics);

        var timeZone = TimeZoneInfo.Create(query.TimeZone);
        var rows = await repository.GetAppointmentRows(query.Period, companyId, cancellationToken);

        return PeakHoursCalculator.Calculate(rows, query.Period, timeZone);
    }
}
