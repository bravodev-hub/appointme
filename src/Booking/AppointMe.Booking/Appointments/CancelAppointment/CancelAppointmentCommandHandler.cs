using AppointMe.Booking.Appointments.Database;
using AppointMe.Booking.Database;

namespace AppointMe.Booking.Appointments.CancelAppointment;

public sealed class CancelAppointmentCommandHandler(BookingDbContext dbContext)
{
    public async Task HandleAsync(CancelAppointmentCommand command, CompanyId companyId,
        Principal principal, CancellationToken cancellationToken)
    {
        principal.Require(AppointmentPermissions.Cancel);

        var appointment = await dbContext.Appointments
            .LoadAsync(new AppointmentId(command.AppointmentId), cancellationToken);

        appointment.Cancel();

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
