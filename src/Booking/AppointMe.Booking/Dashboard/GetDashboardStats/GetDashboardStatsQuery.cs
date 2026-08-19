namespace AppointMe.Booking.Dashboard.GetDashboardStats;

public sealed class GetDashboardStatsQuery
{
    public required DateTimeOffsetPeriod Period { get; init; }
    public required DateTimeOffsetPeriod? ComparePeriod { get; init; }
    public required StatsBucket Bucket { get; init; }
    public required string TimeZone { get; init; }
}
