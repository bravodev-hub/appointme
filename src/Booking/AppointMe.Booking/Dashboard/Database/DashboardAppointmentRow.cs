using AppointMe.Booking.Appointments;

namespace AppointMe.Booking.Dashboard.Database;

public sealed record DashboardAppointmentRow
{
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }
    public required AppointmentStatus Status { get; init; }
    public required Guid ProviderId { get; init; }
    public required Guid AttendeeId { get; init; }
    public required bool HasPriorAppointment { get; init; }
}
