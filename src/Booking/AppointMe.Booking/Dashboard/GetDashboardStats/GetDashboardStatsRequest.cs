namespace AppointMe.Booking.Dashboard.GetDashboardStats;

public sealed class GetDashboardStatsRequest
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public DateTimeOffset? CompareFrom { get; init; }
    public DateTimeOffset? CompareTo { get; init; }
    public required string Bucket { get; init; }
    public required string TimeZone { get; init; }
}

public static class GetDashboardStatsRequestExtensions
{
    extension(GetDashboardStatsRequest request)
    {
        public GetDashboardStatsQuery ToQuery()
        {
            if (request.CompareFrom.HasValue != request.CompareTo.HasValue)
            {
                throw new ValidationException("CompareFrom and CompareTo must be provided together.");
            }

            return new GetDashboardStatsQuery
            {
                Period = DateTimeOffsetPeriod.Create(request.From, request.To),
                ComparePeriod = request is { CompareFrom: not null, CompareTo: not null }
                    ? DateTimeOffsetPeriod.Create(request.CompareFrom.Value, request.CompareTo.Value)
                    : null,
                Bucket = StatsBucket.Parse(request.Bucket),
                TimeZone = request.TimeZone
            };
        }
    }
}
