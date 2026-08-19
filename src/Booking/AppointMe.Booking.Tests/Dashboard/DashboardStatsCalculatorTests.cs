using System.Globalization;
using AppointMe.Booking.Dashboard.Database;
using AppointMe.Booking.Dashboard.GetDashboardStats;

namespace AppointMe.Booking.Tests.Dashboard;

public class DashboardStatsCalculatorTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    private static DateTimeOffsetPeriod Period(string startUtc, string endUtc) =>
        DateTimeOffsetPeriod.Create(
            DateTimeOffset.Parse(startUtc, DateTimeFormatInfo.InvariantInfo).ToUniversalTime(),
            DateTimeOffset.Parse(endUtc, DateTimeFormatInfo.InvariantInfo).ToUniversalTime());

    private static DashboardAppointmentRow NewRow(
        string startUtc,
        int durationMinutes = 30,
        AppointmentStatus status = AppointmentStatus.Scheduled,
        Guid? providerId = null,
        bool hasPrior = false)
    {
        var start = DateTimeOffset.Parse(startUtc, DateTimeFormatInfo.InvariantInfo).ToUniversalTime();
        return new DashboardAppointmentRow
        {
            Start = start,
            End = start.AddMinutes(durationMinutes),
            Status = status,
            ProviderId = providerId ?? NewId(),
            AttendeeId = NewId(),
            HasPriorAppointment = hasPrior
        };
    }

    private static ServiceProviderDto NewProvider(string firstName = "Isla", string? lastName = "Morgan") =>
        new() { Id = NewId(), FirstName = firstName, LastName = lastName };

    [Fact]
    public void should_count_scheduled_appointments_and_exclude_cancelled()
    {
        var period = Period("2026-05-04T00:00Z", "2026-05-05T00:00Z");
        var rows = new[]
        {
            NewRow("2026-05-04T09:00Z"),
            NewRow("2026-05-04T10:00Z"),
            NewRow("2026-05-04T11:00Z", status: AppointmentStatus.Cancelled)
        };

        var result = DashboardStatsCalculator.Calculate(
            rows, compareRows: null, providers: [NewProvider()],
            period, comparePeriod: null, StatsBucket.Day, Utc);

        Assert.Equal(2, result.Appointments);
        Assert.Equal(1, Assert.Single(result.TrendBuckets).Cancellations);
        Assert.Null(result.CompareAppointments);
    }

    [Fact]
    public void should_bucket_appointments_by_day_in_company_time_zone()
    {
        // 22:30Z is 23:30 BST (same day); 23:30Z is 00:30 BST next day.
        var period = Period("2026-05-03T23:00Z", "2026-05-05T23:00Z"); // May 4–5 in London
        var rows = new[]
        {
            NewRow("2026-05-04T22:30Z"),
            NewRow("2026-05-04T23:30Z")
        };

        var result = DashboardStatsCalculator.Calculate(
            rows, null, [NewProvider()], period, null, StatsBucket.Day, London);

        Assert.Equal(2, result.TrendBuckets.Count);
        Assert.Equal(new DateOnly(2026, 5, 4), result.TrendBuckets[0].BucketStart);
        Assert.Equal(1, result.TrendBuckets[0].Appointments);
        Assert.Equal(new DateOnly(2026, 5, 5), result.TrendBuckets[1].BucketStart);
        Assert.Equal(1, result.TrendBuckets[1].Appointments);
    }

    [Fact]
    public void should_start_week_buckets_on_monday()
    {
        var period = Period("2026-05-04T00:00Z", "2026-05-18T00:00Z"); // two ISO weeks
        var rows = new[] { NewRow("2026-05-10T10:00Z") }; // Sunday of week starting Mon May 4

        var result = DashboardStatsCalculator.Calculate(
            rows, null, [NewProvider()], period, null, StatsBucket.Week, Utc);

        Assert.Equal(2, result.TrendBuckets.Count);
        Assert.Equal(new DateOnly(2026, 5, 4), result.TrendBuckets[0].BucketStart);
        Assert.Equal(1, result.TrendBuckets[0].Appointments);
        Assert.Equal(new DateOnly(2026, 5, 11), result.TrendBuckets[1].BucketStart);
        Assert.Equal(0, result.TrendBuckets[1].Appointments);
    }

    [Fact]
    public void should_include_empty_buckets_for_days_without_appointments()
    {
        var period = Period("2026-05-04T00:00Z", "2026-05-11T00:00Z");
        var rows = new[] { NewRow("2026-05-06T10:00Z") };

        var result = DashboardStatsCalculator.Calculate(
            rows, null, [NewProvider()], period, null, StatsBucket.Day, Utc);

        Assert.Equal(7, result.TrendBuckets.Count);
        Assert.Equal(1, result.TrendBuckets.Sum(bucket => bucket.Appointments));
        Assert.Equal(6, result.TrendBuckets.Count(bucket => bucket.Appointments == 0));
    }

    [Fact]
    public void should_compute_returning_client_rate_from_prior_flag()
    {
        var period = Period("2026-05-04T00:00Z", "2026-05-05T00:00Z");
        var rows = new[]
        {
            NewRow("2026-05-04T09:00Z", hasPrior: true),
            NewRow("2026-05-04T10:00Z", hasPrior: true),
            NewRow("2026-05-04T11:00Z", hasPrior: true),
            NewRow("2026-05-04T12:00Z", hasPrior: false),
            // Cancelled rows must not count toward the rate:
            NewRow("2026-05-04T13:00Z", status: AppointmentStatus.Cancelled, hasPrior: true)
        };

        var result = DashboardStatsCalculator.Calculate(
            rows, null, [NewProvider()], period, null, StatsBucket.Day, Utc);

        Assert.Equal(3, result.ReturningAppointments);
        Assert.Equal(75.0, result.ReturningClientRatePercent);
    }

    [Fact]
    public void should_return_null_rates_when_no_appointments_and_no_providers()
    {
        var period = Period("2026-05-04T00:00Z", "2026-05-05T00:00Z");

        var result = DashboardStatsCalculator.Calculate(
            [], null, providers: [], period, null, StatsBucket.Day, Utc);

        Assert.Equal(0, result.Appointments);
        Assert.Equal(0, result.CapacityMinutes);
        Assert.Null(result.UtilizationPercent);
        Assert.Null(result.ReturningClientRatePercent);
        Assert.Empty(result.StaffLoad);
    }

    [Fact]
    public void should_compute_capacity_excluding_sundays()
    {
        // Mon May 4 → Mon May 11: 7 days, one Sunday → 6 business days.
        var period = Period("2026-05-04T00:00Z", "2026-05-11T00:00Z");
        var rows = new[] { NewRow("2026-05-04T09:00Z", durationMinutes: 60) };

        var result = DashboardStatsCalculator.Calculate(
            rows, null, [NewProvider()], period, null, StatsBucket.Day, Utc);

        Assert.Equal(6 * 8 * 60, result.CapacityMinutes);
        Assert.Equal(60, result.BookedMinutes);
        Assert.Equal(Math.Round(60 * 100.0 / (6 * 8 * 60), 1), result.UtilizationPercent);
    }

    [Fact]
    public void should_include_idle_providers_with_zero_bookings_sorted_by_bookings()
    {
        var period = Period("2026-05-04T00:00Z", "2026-05-05T00:00Z");
        var busy = NewProvider("Rafael", "Costa");
        var idle = NewProvider("Mira", "Solé");
        var rows = new[] { NewRow("2026-05-04T09:00Z", providerId: busy.Id) };

        var result = DashboardStatsCalculator.Calculate(
            rows, null, [idle, busy], period, null, StatsBucket.Day, Utc);

        Assert.Equal(2, result.StaffLoad.Count);
        Assert.Equal("Rafael Costa", result.StaffLoad[0].Name);
        Assert.Equal(1, result.StaffLoad[0].Bookings);
        Assert.Equal("Mira Solé", result.StaffLoad[1].Name);
        Assert.Equal(0, result.StaffLoad[1].Bookings);
        Assert.Equal(1 * 8 * 60, result.StaffLoad[1].CapacityMinutes);
    }

    [Fact]
    public void should_compute_compare_kpis_and_buckets_when_compare_rows_provided()
    {
        var period = Period("2026-05-04T00:00Z", "2026-05-05T00:00Z");
        var comparePeriod = Period("2026-05-03T00:00Z", "2026-05-04T00:00Z");
        var rows = new[] { NewRow("2026-05-04T09:00Z"), NewRow("2026-05-04T10:00Z") };
        var compareRows = new[] { NewRow("2026-05-03T09:00Z") };

        var result = DashboardStatsCalculator.Calculate(
            rows, compareRows, [NewProvider()], period, comparePeriod, StatsBucket.Day, Utc);

        Assert.Equal(2, result.Appointments);
        Assert.Equal(1, result.CompareAppointments);
        Assert.Equal(1, Assert.Single(result.CompareTrendBuckets).Appointments);
    }
}
