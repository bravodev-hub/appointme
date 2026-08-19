namespace AppointMe.Booking.Dashboard.GetPeakHours;

public sealed record PeakHoursDayDto
{
    /// <summary>1 = Monday … 7 = Sunday.</summary>
    public required int IsoWeekday { get; init; }

    /// <summary>24 entries; index is the hour of day in the company time zone.</summary>
    public required IReadOnlyList<double> HourlyAverages { get; init; }
}
