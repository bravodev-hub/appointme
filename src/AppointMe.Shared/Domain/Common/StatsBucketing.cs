namespace AppointMe.Shared.Domain.Common;

public static class StatsBucketing
{
    public static DateOnly BucketStartFor(DateTimeOffset instant, StatsBucket bucket, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(instant, timeZone);
        var date = DateOnly.FromDateTime(local.Date);
        return bucket switch
        {
            StatsBucket.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
            StatsBucket.Month => new DateOnly(date.Year, date.Month, 1),
            _ => date
        };
    }

    public static IEnumerable<DateOnly> EnumerateBucketStarts(DateTimeOffsetPeriod period, StatsBucket bucket,
        TimeZoneInfo timeZone)
    {
        var first = BucketStartFor(period.Start, bucket, timeZone);
        var last = BucketStartFor(period.End.AddTicks(-1), bucket, timeZone);
        for (var current = first; current <= last; current = Advance(current, bucket))
        {
            yield return current;
        }
    }

    public static int CountBusinessDays(DateTimeOffsetPeriod period, TimeZoneInfo timeZone)
    {
        var firstDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(period.Start, timeZone).Date);
        var lastDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(period.End.AddTicks(-1), timeZone).Date);
        var count = 0;
        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            if (day.DayOfWeek != DayOfWeek.Sunday)
            {
                count++;
            }
        }

        return count;
    }

    private static DateOnly Advance(DateOnly bucketStart, StatsBucket bucket) => bucket switch
    {
        StatsBucket.Week => bucketStart.AddDays(7),
        StatsBucket.Month => bucketStart.AddMonths(1),
        _ => bucketStart.AddDays(1)
    };
}
