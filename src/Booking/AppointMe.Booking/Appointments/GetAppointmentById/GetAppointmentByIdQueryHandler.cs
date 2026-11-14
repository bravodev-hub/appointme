using AppointMe.Booking.Appointments.Database;

namespace AppointMe.Booking.Appointments.GetAppointmentById;

public sealed class GetAppointmentByIdQueryHandler(AppointmentsRepository repository)
{
    public async Task<AppointmentDto> HandleAsync(GetAppointmentByIdQuery query,
        CompanyId companyId, Principal principal, CancellationToken cancellationToken)
    {
        principal.Require(AppointmentPermissions.View);

        var appointment = await repository.LoadById(new AppointmentId(query.AppointmentId), companyId, cancellationToken);
        return appointment.Enrich();
    }
}
