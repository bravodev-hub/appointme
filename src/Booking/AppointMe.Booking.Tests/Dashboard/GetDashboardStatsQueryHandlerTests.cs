using System.Globalization;
using AppointMe.Booking.Dashboard.GetDashboardStats;
using AppointMe.Shared.Authorization.Permissions;
using AppointMe.Shared.Authorization.Principals;
using AppointMe.Shared.Authorization.Roles;

namespace AppointMe.Booking.Tests.Dashboard;

public class GetDashboardStatsQueryHandlerTests
{
    private sealed class NoPermissionsPrincipal : IPrincipal
    {
        public bool HasRole(Role role) => false;
        public bool HasPermission(Permission permission) => false;
    }

    [Fact]
    public async Task should_deny_when_principal_lacks_view_statistics_permission()
    {
        // Repositories are passed as null! on purpose: the permission check must
        // throw before any dependency is touched.
        var handler = new GetDashboardStatsQueryHandler(null!, null!);
        var query = new GetDashboardStatsQuery
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
