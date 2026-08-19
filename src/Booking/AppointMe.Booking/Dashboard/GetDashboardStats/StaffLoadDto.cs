namespace AppointMe.Booking.Dashboard.GetDashboardStats;

public sealed record StaffLoadDto
{
    public required Guid ProviderId { get; init; }
    public required string Name { get; init; }
    public required int Bookings { get; init; }
    public required int BookedMinutes { get; init; }
    public required int CapacityMinutes { get; init; }
}
