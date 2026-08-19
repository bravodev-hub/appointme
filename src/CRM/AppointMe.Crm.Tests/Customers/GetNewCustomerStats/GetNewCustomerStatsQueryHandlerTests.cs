using System.Globalization;
using AppointMe.Crm.Customers.GetNewCustomerStats;
using AppointMe.Shared.Authorization.Permissions;
using AppointMe.Shared.Authorization.Principals;
using AppointMe.Shared.Authorization.Roles;
using AppointMe.Shared.Companies;
using AppointMe.Shared.Domain.Errors;

namespace AppointMe.Crm.Tests.Customers.GetNewCustomerStats;

public class GetNewCustomerStatsQueryHandlerTests
{
    private sealed class NoPermissionsPrincipal : IPrincipal
    {
        public bool HasRole(Role role) => false;
        public bool HasPermission(Permission permission) => false;
    }

    [Fact]
    public async Task should_deny_when_principal_lacks_view_statistics_permission()
    {
        var handler = new GetNewCustomerStatsQueryHandler(null!);
        var query = new GetNewCustomerStatsQuery
        {
            Period = DateTimeOffsetPeriod.Create(
                DateTimeOffset.Parse("2026-05-04T00:00Z", DateTimeFormatInfo.InvariantInfo).ToUniversalTime(),
                DateTimeOffset.Parse("2026-05-05T00:00Z", DateTimeFormatInfo.InvariantInfo).ToUniversalTime()),
            ComparePeriod = null,
            Bucket = StatsBucket.Day,
            TimeZone = "Europe/London"
        };

        await Assert.ThrowsAsync<AccessDeniedException>(() => handler.HandleAsync(
            query, new CompanyId(NewId()), new NoPermissionsPrincipal(), CancellationToken.None));
    }
}
