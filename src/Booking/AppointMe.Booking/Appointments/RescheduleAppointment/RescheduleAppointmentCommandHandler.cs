using AppointMe.Booking.Appointments.Database;
using AppointMe.Booking.Database;
using AppointMe.Booking.ServiceProviders;

namespace AppointMe.Booking.Appointments.RescheduleAppointment;

public sealed class RescheduleAppointmentCommandHandler(BookingDbContext dbContext)
{
    public async Task HandleAsync(RescheduleAppointmentCommand command, CompanyId companyId,
        Principal principal, CancellationToken cancellationToken)
    {
        principal.Require(AppointmentPermissions.Reschedule);

        var appointment = await dbContext.Appointments
            .LoadAsync(new AppointmentId(command.AppointmentId), cancellationToken);

        var providerId = new ServiceProviderId(command.ProviderId);
        var providerExists = await dbContext.ServiceProviders
            .AnyAsync(provider => provider.Id == providerId, cancellationToken);

        if (!providerExists)
        {
            throw new ValidationException("Service provider not found.");
        }

        var timePeriod = DateTimeOffsetPeriod.Create(command.Start, command.End);
        appointment.Reschedule(providerId, timePeriod);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
