using System.Globalization;

namespace AppointMe.Shared.Tests.Domain.Common;

public class StatsBucketingTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    private static DateTimeOffsetPeriod Period(string startUtc, string endUtc) =>
        DateTimeOffsetPeriod.Create(
            DateTimeOffset.Parse(startUtc, DateTimeFormatInfo.InvariantInfo).ToUniversalTime(),
            DateTimeOffset.Parse(endUtc, DateTimeFormatInfo.InvariantInfo).ToUniversalTime());

    [Fact]
    public void should_return_same_calendar_date_for_day_bucket()
    {
        var instant = DateTimeOffset.Parse("2026-05-04T15:30Z", DateTimeFormatInfo.InvariantInfo);

        var bucketStart = StatsBucketing.BucketStartFor(instant, StatsBucket.Day, Utc);

        Assert.Equal(new DateOnly(2026, 5, 4), bucketStart);
    }

    [Fact]
    public void should_return_monday_of_the_same_week_for_week_bucket()
    {
        var wednesday = DateTimeOffset.Parse("2026-05-06T10:00Z", DateTimeFormatInfo.InvariantInfo);

        var bucketStart = StatsBucketing.BucketStartFor(wednesday, StatsBucket.Week, Utc);

        Assert.Equal(new DateOnly(2026, 5, 4), bucketStart);
    }

    [Fact]
    public void should_return_previous_monday_for_a_sunday_in_week_bucket()
    {
        // Sunday belongs to the week that started on the Monday six days earlier.
        var sunday = DateTimeOffset.Parse("2026-05-10T10:00Z", DateTimeFormatInfo.InvariantInfo);

        var bucketStart = StatsBucketing.BucketStartFor(sunday, StatsBucket.Week, Utc);

        Assert.Equal(new DateOnly(2026, 5, 4), bucketStart);
    }

    [Fact]
    public void should_return_first_day_of_month_for_month_bucket()
    {
        var midMonth = DateTimeOffset.Parse("2026-05-19T00:00Z", DateTimeFormatInfo.InvariantInfo);

        var bucketStart = StatsBucketing.BucketStartFor(midMonth, StatsBucket.Month, Utc);

        Assert.Equal(new DateOnly(2026, 5, 1), bucketStart);
    }

    [Fact]
    public void should_enumerate_consecutive_daily_bucket_starts_for_a_day_range()
    {
        var period = Period("2026-05-04T00:00Z", "2026-05-07T00:00Z"); // Mon, Tue, Wed

        var bucketStarts = StatsBucketing.EnumerateBucketStarts(period, StatsBucket.Day, Utc).ToList();

        Assert.Equal(
            [new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 6)],
            bucketStarts);
    }

    [Fact]
    public void should_enumerate_consecutive_monthly_bucket_starts_for_a_month_range()
    {
        var period = Period("2026-03-01T00:00Z", "2026-06-01T00:00Z"); // Mar, Apr, May

        var bucketStarts = StatsBucketing.EnumerateBucketStarts(period, StatsBucket.Month, Utc).ToList();

        Assert.Equal(
            [new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 1)],
            bucketStarts);
    }

    [Fact]
    public void should_enumerate_consecutive_daily_buckets_with_no_gap_or_duplicate_across_dst_spring_transition()
    {
        // Europe/London clocks spring forward at 2026-03-29T01:00Z (01:00 GMT -> 02:00 BST).
        // 2026-03-27T00:00 local is still GMT (+00:00); 2026-03-31T00:00 local is already BST (+01:00).
        var period = Period("2026-03-27T00:00Z", "2026-03-30T23:00Z"); // Mar 27 00:00 -> Mar 31 00:00 London local

        var bucketStarts = StatsBucketing.EnumerateBucketStarts(period, StatsBucket.Day, London).ToList();

        Assert.Equal(
            [
                new DateOnly(2026, 3, 27),
                new DateOnly(2026, 3, 28),
                new DateOnly(2026, 3, 29),
                new DateOnly(2026, 3, 30)
            ],
            bucketStarts);
    }

    [Fact]
    public void should_yield_a_single_bucket_for_the_23_hour_local_day_when_dst_starts()
    {
        // 2026-03-29 is a 23-hour day in London: clocks skip 01:00-02:00 local when BST starts,
        // so the calendar day still spans exactly one bucket even though real elapsed time is 23h.
        var period = Period("2026-03-29T00:00Z", "2026-03-29T23:00Z"); // Mar 29 00:00 -> Mar 30 00:00 London local

        var bucketStarts = StatsBucketing.EnumerateBucketStarts(period, StatsBucket.Day, London).ToList();

        Assert.Equal([new DateOnly(2026, 3, 29)], bucketStarts);
    }

    [Fact]
    public void should_count_six_business_days_in_a_full_week_excluding_sunday()
    {
        var period = Period("2026-05-04T00:00Z", "2026-05-11T00:00Z"); // Mon 4 -> Sun 10 inclusive

        var businessDays = StatsBucketing.CountBusinessDays(period, Utc);

        Assert.Equal(6, businessDays);
    }

    [Fact]
    public void should_count_business_days_for_a_range_starting_and_ending_mid_week_without_a_sunday()
    {
        var period = Period("2026-05-06T00:00Z", "2026-05-09T00:00Z"); // Wed, Thu, Fri

        var businessDays = StatsBucketing.CountBusinessDays(period, Utc);

        Assert.Equal(3, businessDays);
    }

    [Fact]
    public void should_count_business_days_for_a_range_starting_and_ending_mid_week_spanning_a_sunday()
    {
        var period = Period("2026-05-08T00:00Z", "2026-05-13T00:00Z"); // Fri 8 -> Tue 12 inclusive, one Sunday

        var businessDays = StatsBucketing.CountBusinessDays(period, Utc);

        Assert.Equal(4, businessDays);
    }
}
