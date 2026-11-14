using System.Globalization;
using AppointMe.Booking.Dashboard.GetPeakHours;
using AppointMe.Shared.Authorization.Principals;

namespace AppointMe.Booking.Tests.Dashboard;

public class GetPeakHoursQueryHandlerTests
{
    [Fact]
    public async Task should_deny_when_principal_lacks_view_statistics_permission()
    {
        var handler = new GetPeakHoursQueryHandler(null!);
        var query = new GetPeakHoursQuery
        {
            Period = DateTimeOffsetPeriod.Create(
                DateTimeOffset.Parse("2026-04-13T00:00Z", DateTimeFormatInfo.InvariantInfo).ToUniversalTime(),
                DateTimeOffset.Parse("2026-05-11T00:00Z", DateTimeFormatInfo.InvariantInfo).ToUniversalTime()),
            TimeZone = "Europe/London"
        };

        await Assert.ThrowsAsync<AccessDeniedException>(() => handler.HandleAsync(
            query, new CompanyId(NewId()), new AnonymousPrincipal(), CancellationToken.None));
    }
}
