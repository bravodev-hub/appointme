using AppointMe.Crm.Customers.Database;

namespace AppointMe.Crm.Customers.GetNewCustomerStats;

public sealed class GetNewCustomerStatsQueryHandler(CustomersRepository repository)
{
    public async Task<GetNewCustomerStatsResponse> HandleAsync(GetNewCustomerStatsQuery query,
        CompanyId companyId, IPrincipal principal, CancellationToken cancellationToken)
    {
        principal.Require(CustomerPermissions.ViewStatistics);

        var timeZone = TimeZoneInfo.Create(query.TimeZone);
        var registrations = await repository.GetRegistrationDates(query.Period, companyId, cancellationToken);
        var compareRegistrations = query.ComparePeriod is not null
            ? await repository.GetRegistrationDates(query.ComparePeriod, companyId, cancellationToken)
            : null;

        return NewCustomerStatsCalculator.Calculate(
            registrations, compareRegistrations, query.Period, query.ComparePeriod, query.Bucket, timeZone);
    }
}
