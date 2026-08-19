namespace AppointMe.Crm.Customers.GetNewCustomerStats;

public sealed class GetNewCustomerStatsRequest
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public DateTimeOffset? CompareFrom { get; init; }
    public DateTimeOffset? CompareTo { get; init; }
    public required string Bucket { get; init; }
    public required string TimeZone { get; init; }
}

public static class GetNewCustomerStatsRequestExtensions
{
    extension(GetNewCustomerStatsRequest request)
    {
        public GetNewCustomerStatsQuery ToQuery()
        {
            if (request.CompareFrom.HasValue != request.CompareTo.HasValue)
            {
                throw new ValidationException("CompareFrom and CompareTo must be provided together.");
            }

            return new GetNewCustomerStatsQuery
            {
                Period = DateTimeOffsetPeriod.Create(request.From, request.To),
                ComparePeriod = request.CompareFrom.HasValue && request.CompareTo.HasValue
                    ? DateTimeOffsetPeriod.Create(request.CompareFrom.Value, request.CompareTo.Value)
                    : null,
                Bucket = StatsBucket.Parse(request.Bucket),
                TimeZone = request.TimeZone
            };
        }
    }
}
