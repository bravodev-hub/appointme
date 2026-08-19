using System.Globalization;
using AppointMe.Crm.Customers.GetNewCustomerStats;

namespace AppointMe.Crm.Tests.Customers.GetNewCustomerStats;

public class NewCustomerStatsCalculatorTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    private static DateTimeOffsetPeriod Period(string startUtc, string endUtc) =>
        DateTimeOffsetPeriod.Create(
            DateTimeOffset.Parse(startUtc, DateTimeFormatInfo.InvariantInfo).ToUniversalTime(),
            DateTimeOffset.Parse(endUtc, DateTimeFormatInfo.InvariantInfo).ToUniversalTime());

    private static DateTimeOffset At(string instantUtc) =>
        DateTimeOffset.Parse(instantUtc, DateTimeFormatInfo.InvariantInfo).ToUniversalTime();

    [Fact]
    public void should_bucket_registrations_by_day_in_company_time_zone()
    {
        // 23:30Z on May 4 is 00:30 BST on May 5 in London.
        var period = Period("2026-05-03T23:00Z", "2026-05-05T23:00Z");
        var registrations = new[] { At("2026-05-04T10:00Z"), At("2026-05-04T23:30Z") };

        var result = NewCustomerStatsCalculator.Calculate(
            registrations, compareRegistrations: null, period, comparePeriod: null, StatsBucket.Day, London);

        Assert.Equal(2, result.Total);
        Assert.Null(result.CompareTotal);
        Assert.Equal(2, result.Buckets.Count);
        Assert.Equal(new DateOnly(2026, 5, 4), result.Buckets[0].BucketStart);
        Assert.Equal(1, result.Buckets[0].Count);
        Assert.Equal(new DateOnly(2026, 5, 5), result.Buckets[1].BucketStart);
        Assert.Equal(1, result.Buckets[1].Count);
    }

    [Fact]
    public void should_include_empty_buckets_for_days_without_registrations()
    {
        var period = Period("2026-05-04T00:00Z", "2026-05-11T00:00Z");
        var registrations = new[] { At("2026-05-06T09:00Z") };

        var result = NewCustomerStatsCalculator.Calculate(
            registrations, null, period, null, StatsBucket.Day, Utc);

        Assert.Equal(7, result.Buckets.Count);
        Assert.Equal(1, result.Buckets.Sum(bucket => bucket.Count));
    }

    [Fact]
    public void should_compute_compare_buckets_only_when_compare_range_provided()
    {
        var period = Period("2026-05-04T00:00Z", "2026-05-05T00:00Z");
        var comparePeriod = Period("2026-05-03T00:00Z", "2026-05-04T00:00Z");
        var registrations = new[] { At("2026-05-04T09:00Z") };
        var compareRegistrations = new[] { At("2026-05-03T09:00Z"), At("2026-05-03T10:00Z") };

        var result = NewCustomerStatsCalculator.Calculate(
            registrations, compareRegistrations, period, comparePeriod, StatsBucket.Day, Utc);

        Assert.Equal(1, result.Total);
        Assert.Equal(2, result.CompareTotal);
        Assert.Equal(2, Assert.Single(result.CompareBuckets).Count);
    }
}
