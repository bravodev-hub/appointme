namespace AppointMe.Booking.Dashboard.GetPeakHours;

public sealed class GetPeakHoursRequest
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required string TimeZone { get; init; }
}

public static class GetPeakHoursRequestExtensions
{
    extension(GetPeakHoursRequest request)
    {
        public GetPeakHoursQuery ToQuery()
        {
            return new GetPeakHoursQuery
            {
                Period = DateTimeOffsetPeriod.Create(request.From, request.To),
                TimeZone = request.TimeZone
            };
        }
    }
}
