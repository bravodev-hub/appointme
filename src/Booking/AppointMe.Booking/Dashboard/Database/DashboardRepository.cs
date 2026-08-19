using AppointMe.Booking.Appointments;
using AppointMe.Shared.Database;
using Dapper;

namespace AppointMe.Booking.Dashboard.Database;

public sealed class DashboardRepository(IDbConnectionFactory connectionFactory)
{
    // HasPriorAppointment is computed relative to the fetched range's own start,
    // so the same method serves current and comparison ranges. The EXISTS probe is
    // also fetched (unused) for the peak-hours window — acceptable at per-tenant
    // scale; split into a second leaner query if it ever shows up in profiles.
    private const string AppointmentRowsSql =
        """
        SELECT Appointments.[Start],
               Appointments.[End],
               Appointments.[Status],
               Appointments.[ProviderId],
               Appointments.[AttendeeId],
               IIF(EXISTS (SELECT 1
                                 FROM [booking].[Appointments] Prior
                                 WHERE Prior.[CompanyId] = @CompanyId
                                   AND Prior.[AttendeeId] = Appointments.[AttendeeId]
                                   AND Prior.[Start] < @From
                                   AND Prior.[Status] = @ScheduledStatus), 1, 0) AS [HasPriorAppointment]
        FROM [booking].[Appointments] Appointments
        WHERE Appointments.[CompanyId] = @CompanyId
          AND Appointments.[Start] >= @From
          AND Appointments.[Start] < @To
          -- Only count appointments of active providers: capacity and staffLoad are both
          -- sourced from ServiceProvidersRepository.GetAll, which filters IsDeleted = 0.
          -- Without this, a soft-deleted provider's appointments would inflate the
          -- numerator while contributing no capacity, pushing utilization above 100%
          -- and making sum(staffLoad.bookings) disagree with stats.appointments.
          AND EXISTS (SELECT 1
                      FROM [booking].[ServiceProviders] Providers
                      WHERE Providers.[Id] = Appointments.[ProviderId]
                        AND Providers.[CompanyId] = @CompanyId
                        AND Providers.[IsDeleted] = 0)
        """;

    public async Task<IReadOnlyList<DashboardAppointmentRow>> GetAppointmentRows(DateTimeOffsetPeriod range,
        CompanyId companyId, CancellationToken cancellationToken)
    {
        using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var commandDefinition = new CommandDefinition(AppointmentRowsSql, new
        {
            CompanyId = companyId.Value,
            From = range.Start,
            To = range.End,
            ScheduledStatus = nameof(AppointmentStatus.Scheduled)
        }, cancellationToken: cancellationToken);
        var results = await connection.QueryAsync<DashboardAppointmentRow>(commandDefinition);
        return results.ToList();
    }
}
