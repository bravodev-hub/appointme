namespace AppointMe.Booking.Dashboard.GetDashboardStats;

public sealed record GetDashboardStatsResponse
{
    public required int Appointments { get; init; }
    public required int? CompareAppointments { get; init; }
    public required int BookedMinutes { get; init; }
    public required int? CompareBookedMinutes { get; init; }
    public required int CapacityMinutes { get; init; }
    public required double? UtilizationPercent { get; init; }
    public required double? CompareUtilizationPercent { get; init; }
    public required int ReturningAppointments { get; init; }
    public required double? ReturningClientRatePercent { get; init; }
    public required double? CompareReturningClientRatePercent { get; init; }
    public required IReadOnlyList<TrendBucketDto> TrendBuckets { get; init; }
    public required IReadOnlyList<TrendBucketDto> CompareTrendBuckets { get; init; }
    public required IReadOnlyList<StaffLoadDto> StaffLoad { get; init; }
}
