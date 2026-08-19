using System.Globalization;
using AppointMe.Booking.Dashboard.Database;
using AppointMe.Booking.Dashboard.GetPeakHours;

namespace AppointMe.Booking.Tests.Dashboard;

public class PeakHoursCalculatorTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    // 28 days: Mon Apr 13 → Mon May 11 (exclusive), i.e. exactly 4 weeks.
    private static readonly DateTimeOffsetPeriod FourWeeks = DateTimeOffsetPeriod.Create(
        DateTimeOffset.Parse("2026-04-13T00:00Z", DateTimeFormatInfo.InvariantInfo).ToUniversalTime(),
        DateTimeOffset.Parse("2026-05-11T00:00Z", DateTimeFormatInfo.InvariantInfo).ToUniversalTime());

    private static DashboardAppointmentRow NewRow(string startUtc,
        AppointmentStatus status = AppointmentStatus.Scheduled)
    {
        var start = DateTimeOffset.Parse(startUtc, DateTimeFormatInfo.InvariantInfo).ToUniversalTime();
        return new DashboardAppointmentRow
        {
            Start = start,
            End = start.AddMinutes(30),
            Status = status,
            ProviderId = NewId(),
            AttendeeId = NewId(),
            HasPriorAppointment = false
        };
    }

    [Fact]
    public void should_return_seven_days_with_24_hourly_averages_each()
    {
        var result = PeakHoursCalculator.Calculate([], FourWeeks, Utc);

        Assert.Equal(7, result.Days.Count);
        Assert.All(result.Days, day => Assert.Equal(24, day.HourlyAverages.Count));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], result.Days.Select(day => day.IsoWeekday));
    }

    [Fact]
    public void should_average_hourly_counts_over_the_number_of_weeks()
    {
        // 8 appointments on Mondays at 10:00 UTC across 4 weeks → average 2.0.
        var rows = new[]
        {
            NewRow("2026-04-13T10:00Z"), NewRow("2026-04-13T10:30Z"),
            NewRow("2026-04-20T10:00Z"), NewRow("2026-04-20T10:30Z"),
            NewRow("2026-04-27T10:00Z"), NewRow("2026-04-27T10:30Z"),
            NewRow("2026-05-04T10:00Z"), NewRow("2026-05-04T10:30Z")
        };

        var result = PeakHoursCalculator.Calculate(rows, FourWeeks, Utc);

        var monday = result.Days.Single(day => day.IsoWeekday == 1);
        Assert.Equal(2.0, monday.HourlyAverages[10]);
        Assert.Equal(0.0, monday.HourlyAverages[11]);
    }

    [Fact]
    public void should_bucket_by_weekday_and_hour_in_company_time_zone()
    {
        // 08:30Z on Mon May 4 is 09:30 BST → hour 9 in London.
        var rows = new[] { NewRow("2026-05-04T08:30Z") };

        var result = PeakHoursCalculator.Calculate(rows, FourWeeks, London);

        var monday = result.Days.Single(day => day.IsoWeekday == 1);
        Assert.Equal(0.0, monday.HourlyAverages[8]);
        Assert.Equal(Math.Round(1 / 4.0, 1), monday.HourlyAverages[9]);
    }

    [Fact]
    public void should_exclude_cancelled_appointments()
    {
        var rows = new[] { NewRow("2026-05-04T10:00Z", AppointmentStatus.Cancelled) };

        var result = PeakHoursCalculator.Calculate(rows, FourWeeks, Utc);

        Assert.All(result.Days, day => Assert.All(day.HourlyAverages, average => Assert.Equal(0.0, average)));
    }
}
