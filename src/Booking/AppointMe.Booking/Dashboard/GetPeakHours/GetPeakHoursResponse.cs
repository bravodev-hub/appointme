namespace AppointMe.Booking.Dashboard.GetPeakHours;

public sealed record GetPeakHoursResponse
{
    public required IReadOnlyList<PeakHoursDayDto> Days { get; init; }
}
