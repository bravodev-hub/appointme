using AppointMe.Shared.Authorization.Permissions;

namespace AppointMe.Booking.Appointments;

public static class AppointmentPermissions
{
    private const string Appointments = "appointments";

    public static readonly Permission View = new(Appointments, "view");
    public static readonly Permission Schedule = new(Appointments, "schedule");
    public static readonly Permission Reschedule = new(Appointments, "reschedule");
    public static readonly Permission Cancel = new(Appointments, "cancel");
    public static readonly Permission ViewStatistics = new($"{Appointments}.statistics", "view");
}
