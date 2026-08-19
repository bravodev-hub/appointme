using AppointMe.Booking.Appointments;
using AppointMe.Booking.Dashboard.Database;

namespace AppointMe.Booking.Dashboard.GetPeakHours;

public static class PeakHoursCalculator
{
    public static GetPeakHoursResponse Calculate(
        IReadOnlyList<DashboardAppointmentRow> rows,
        DateTimeOffsetPeriod period,
        TimeZoneInfo timeZone)
    {
        var weeks = Math.Max((period.End - period.Start).TotalDays / 7.0, 1.0);
        var counts = new int[7, 24];

        foreach (var row in rows.Where(row => row.Status == AppointmentStatus.Scheduled))
        {
            var local = TimeZoneInfo.ConvertTime(row.Start, timeZone);
            var weekdayIndex = ((int)local.DayOfWeek + 6) % 7;
            counts[weekdayIndex, local.Hour]++;
        }

        var days = Enumerable.Range(0, 7)
            .Select(weekdayIndex => new PeakHoursDayDto
            {
                IsoWeekday = weekdayIndex + 1,
                HourlyAverages = Enumerable.Range(0, 24)
                    .Select(hour => Math.Round(counts[weekdayIndex, hour] / weeks, 1))
                    .ToList()
            })
            .ToList();

        return new GetPeakHoursResponse { Days = days };
    }
}
