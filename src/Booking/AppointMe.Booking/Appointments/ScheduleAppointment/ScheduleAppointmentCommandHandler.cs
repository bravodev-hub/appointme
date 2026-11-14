using AppointMe.Booking.Attendees;
using AppointMe.Booking.Database;
using AppointMe.Booking.ServiceProviders;

namespace AppointMe.Booking.Appointments.ScheduleAppointment;

public sealed class ScheduleAppointmentCommandHandler(
    BookingDbContext dbContext,
    TimeProvider timeProvider
)
{
    public async Task<ScheduleAppointmentResponse> HandleAsync(ScheduleAppointmentCommand command,
        CompanyId companyId, Principal principal, CancellationToken cancellationToken)
    {
        principal.Require(AppointmentPermissions.Schedule);

        var providerId = new ServiceProviderId(command.ProviderId);
        var providerExists = await dbContext.ServiceProviders
            .AnyAsync(provider => provider.Id == providerId, cancellationToken);

        if (!providerExists)
        {
            throw new ValidationException("Service provider not found.");
        }

        var attendeeId = new AttendeeId(command.AttendeeId);
        var attendeeExists = await dbContext.Attendees
            .AnyAsync(attendee => attendee.Id == attendeeId, cancellationToken);

        if (!attendeeExists)
        {
            throw new ValidationException("Attendee not found.");
        }

        var appointment = Appointment.Schedule(
            companyId: companyId,
            providerId: providerId,
            attendeeId: attendeeId,
            period: DateTimeOffsetPeriod.Create(command.Start, command.End),
            notes: LongString.CreateOrNull(command.Notes),
            scheduledAt: timeProvider.GetUtcNow());

        await dbContext.Appointments.AddAsync(appointment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ScheduleAppointmentResponse(appointment.Id.Value);
    }
}
