using AppointMe.Booking.Appointments.Database;

namespace AppointMe.Booking.Appointments.GetAppointments;

public sealed class GetAppointmentsQueryHandler(AppointmentsRepository repository)
{
    public async Task<IReadOnlyList<AppointmentDto>> HandleAsync(GetAppointmentsQuery query,
        CompanyId companyId, Principal principal, CancellationToken cancellationToken)
    {
        principal.Require(AppointmentPermissions.View);

        var range = DateTimeOffsetPeriod.Create(query.Start, query.End);
        var appointments = await repository.GetByDateRange(range, companyId, cancellationToken);

        return appointments.Select(appointment => appointment.Enrich()).ToList();
    }
}
