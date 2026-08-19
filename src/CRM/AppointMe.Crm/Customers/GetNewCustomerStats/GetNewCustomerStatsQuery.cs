namespace AppointMe.Crm.Customers.GetNewCustomerStats;

public sealed class GetNewCustomerStatsQuery
{
    public required DateTimeOffsetPeriod Period { get; init; }
    public required DateTimeOffsetPeriod? ComparePeriod { get; init; }
    public required StatsBucket Bucket { get; init; }
    public required string TimeZone { get; init; }
}
