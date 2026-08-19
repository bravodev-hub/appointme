namespace AppointMe.Crm.Customers.GetNewCustomerStats;

public sealed record GetNewCustomerStatsResponse
{
    public required int Total { get; init; }
    public required int? CompareTotal { get; init; }
    public required IReadOnlyList<NewCustomerBucketDto> Buckets { get; init; }
    public required IReadOnlyList<NewCustomerBucketDto> CompareBuckets { get; init; }
}
