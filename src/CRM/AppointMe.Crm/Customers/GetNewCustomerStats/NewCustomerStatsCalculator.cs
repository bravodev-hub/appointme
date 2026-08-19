namespace AppointMe.Crm.Customers.GetNewCustomerStats;

public static class NewCustomerStatsCalculator
{
    public static GetNewCustomerStatsResponse Calculate(
        IReadOnlyList<DateTimeOffset> registrations,
        IReadOnlyList<DateTimeOffset>? compareRegistrations,
        DateTimeOffsetPeriod period,
        DateTimeOffsetPeriod? comparePeriod,
        StatsBucket bucket,
        TimeZoneInfo timeZone)
    {
        return new GetNewCustomerStatsResponse
        {
            Total = registrations.Count,
            CompareTotal = compareRegistrations?.Count,
            Buckets = BuildBuckets(registrations, period, bucket, timeZone),
            CompareBuckets = compareRegistrations is not null && comparePeriod is not null
                ? BuildBuckets(compareRegistrations, comparePeriod, bucket, timeZone)
                : []
        };
    }

    private static IReadOnlyList<NewCustomerBucketDto> BuildBuckets(
        IReadOnlyList<DateTimeOffset> registrations,
        DateTimeOffsetPeriod period,
        StatsBucket bucket,
        TimeZoneInfo timeZone)
    {
        var countsByBucket = registrations
            .GroupBy(registration => StatsBucketing.BucketStartFor(registration, bucket, timeZone))
            .ToDictionary(group => group.Key, group => group.Count());

        return StatsBucketing.EnumerateBucketStarts(period, bucket, timeZone)
            .Select(bucketStart => new NewCustomerBucketDto
            {
                BucketStart = bucketStart,
                Count = countsByBucket.GetValueOrDefault(bucketStart)
            })
            .ToList();
    }
}
