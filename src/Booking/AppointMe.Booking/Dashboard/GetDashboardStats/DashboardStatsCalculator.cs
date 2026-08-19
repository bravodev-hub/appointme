using AppointMe.Booking.Appointments;
using AppointMe.Booking.Dashboard.Database;
using AppointMe.Booking.ServiceProviders;

namespace AppointMe.Booking.Dashboard.GetDashboardStats;

public static class DashboardStatsCalculator
{
    // Bookable-capacity convention for the template: every active provider is assumed
    // bookable 8 hours per business day (Mon–Sat). Replace with real working hours
    // when a scheduling domain exists.
    private const int BookableHoursPerBusinessDay = 8;

    public static GetDashboardStatsResponse Calculate(
        IReadOnlyList<DashboardAppointmentRow> rows,
        IReadOnlyList<DashboardAppointmentRow>? compareRows,
        IReadOnlyList<ServiceProviderDto> providers,
        DateTimeOffsetPeriod period,
        DateTimeOffsetPeriod? comparePeriod,
        StatsBucket bucket,
        TimeZoneInfo timeZone)
    {
        var current = CalculateKpis(rows, providers.Count, period, timeZone);
        var compare = compareRows is not null && comparePeriod is not null
            ? CalculateKpis(compareRows, providers.Count, comparePeriod, timeZone)
            : null;

        return new GetDashboardStatsResponse
        {
            Appointments = current.Appointments,
            CompareAppointments = compare?.Appointments,
            BookedMinutes = current.BookedMinutes,
            CompareBookedMinutes = compare?.BookedMinutes,
            CapacityMinutes = current.CapacityMinutes,
            UtilizationPercent = current.UtilizationPercent,
            CompareUtilizationPercent = compare?.UtilizationPercent,
            ReturningAppointments = current.ReturningAppointments,
            ReturningClientRatePercent = current.ReturningClientRatePercent,
            CompareReturningClientRatePercent = compare?.ReturningClientRatePercent,
            TrendBuckets = BuildTrendBuckets(rows, period, bucket, timeZone),
            CompareTrendBuckets = compareRows is not null && comparePeriod is not null
                ? BuildTrendBuckets(compareRows, comparePeriod, bucket, timeZone)
                : [],
            StaffLoad = BuildStaffLoad(rows, providers, current.CapacityPerProviderMinutes)
        };
    }

    private sealed record KpiSnapshot(
        int Appointments,
        int BookedMinutes,
        int CapacityMinutes,
        int CapacityPerProviderMinutes,
        double? UtilizationPercent,
        int ReturningAppointments,
        double? ReturningClientRatePercent);

    private static KpiSnapshot CalculateKpis(IReadOnlyList<DashboardAppointmentRow> rows, int providerCount,
        DateTimeOffsetPeriod period, TimeZoneInfo timeZone)
    {
        var scheduled = rows.Where(row => row.Status == AppointmentStatus.Scheduled).ToList();
        var bookedMinutes = (int)scheduled.Sum(row => (row.End - row.Start).TotalMinutes);
        var capacityPerProviderMinutes =
            StatsBucketing.CountBusinessDays(period, timeZone) * BookableHoursPerBusinessDay * 60;
        var capacityMinutes = capacityPerProviderMinutes * providerCount;
        var returning = scheduled.Count(row => row.HasPriorAppointment);

        return new KpiSnapshot(
            Appointments: scheduled.Count,
            BookedMinutes: bookedMinutes,
            CapacityMinutes: capacityMinutes,
            CapacityPerProviderMinutes: capacityPerProviderMinutes,
            UtilizationPercent: capacityMinutes == 0
                ? null
                : Math.Round(bookedMinutes * 100.0 / capacityMinutes, 1),
            ReturningAppointments: returning,
            ReturningClientRatePercent: scheduled.Count == 0
                ? null
                : Math.Round(returning * 100.0 / scheduled.Count, 1));
    }

    private static IReadOnlyList<TrendBucketDto> BuildTrendBuckets(
        IReadOnlyList<DashboardAppointmentRow> rows,
        DateTimeOffsetPeriod period,
        StatsBucket bucket,
        TimeZoneInfo timeZone)
    {
        var rowsByBucket = rows
            .GroupBy(row => StatsBucketing.BucketStartFor(row.Start, bucket, timeZone))
            .ToDictionary(group => group.Key, group => group.ToList());

        return StatsBucketing.EnumerateBucketStarts(period, bucket, timeZone)
            .Select(bucketStart =>
            {
                var bucketRows = rowsByBucket.GetValueOrDefault(bucketStart, []);
                return new TrendBucketDto
                {
                    BucketStart = bucketStart,
                    Appointments = bucketRows.Count(row => row.Status == AppointmentStatus.Scheduled),
                    Cancellations = bucketRows.Count(row => row.Status == AppointmentStatus.Cancelled)
                };
            })
            .ToList();
    }

    private static IReadOnlyList<StaffLoadDto> BuildStaffLoad(
        IReadOnlyList<DashboardAppointmentRow> rows,
        IReadOnlyList<ServiceProviderDto> providers,
        int capacityPerProviderMinutes)
    {
        var scheduledByProvider = rows
            .Where(row => row.Status == AppointmentStatus.Scheduled)
            .GroupBy(row => row.ProviderId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return providers
            .Select(provider =>
            {
                var providerRows = scheduledByProvider.GetValueOrDefault(provider.Id, []);
                return new StaffLoadDto
                {
                    ProviderId = provider.Id,
                    Name = provider.Enrich().FullName ?? string.Empty,
                    Bookings = providerRows.Count,
                    BookedMinutes = (int)providerRows.Sum(row => (row.End - row.Start).TotalMinutes),
                    CapacityMinutes = capacityPerProviderMinutes
                };
            })
            .OrderByDescending(staff => staff.Bookings)
            .ThenBy(staff => staff.Name)
            .ToList();
    }
}
