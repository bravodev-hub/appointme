namespace AppointMe.Booking.Dashboard.GetDashboardStats;

public sealed record TrendBucketDto
{
    public required DateOnly BucketStart { get; init; }
    public required int Appointments { get; init; }
    public required int Cancellations { get; init; }
}
