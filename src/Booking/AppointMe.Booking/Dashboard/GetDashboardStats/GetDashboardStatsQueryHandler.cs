using AppointMe.Booking.Appointments;
using AppointMe.Booking.Dashboard.Database;
using AppointMe.Booking.ServiceProviders.Database;

namespace AppointMe.Booking.Dashboard.GetDashboardStats;

public sealed class GetDashboardStatsQueryHandler(
    DashboardRepository repository,
    ServiceProvidersRepository providersRepository)
{
    public async Task<GetDashboardStatsResponse> HandleAsync(GetDashboardStatsQuery query,
        CompanyId companyId, IPrincipal principal, CancellationToken cancellationToken)
    {
        principal.Require(AppointmentPermissions.ViewStatistics);

        var timeZone = TimeZoneInfo.Create(query.TimeZone);
        var rows = await repository.GetAppointmentRows(query.Period, companyId, cancellationToken);
        var compareRows = query.ComparePeriod is not null
            ? await repository.GetAppointmentRows(query.ComparePeriod, companyId, cancellationToken)
            : null;
        var providers = await providersRepository.GetAll(companyId, cancellationToken);

        return DashboardStatsCalculator.Calculate(
            rows, compareRows, providers, query.Period, query.ComparePeriod, query.Bucket, timeZone);
    }
}
