namespace AppointMe.Booking.Dashboard.GetPeakHours;

public sealed class GetPeakHoursQuery
{
    public required DateTimeOffsetPeriod Period { get; init; }
    public required string TimeZone { get; init; }
}
