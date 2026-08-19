using AppointMe.Booking.Appointments;
using AppointMe.Shared.Authorization.Roles;
using AppointMe.Shared.Authorization.Permissions.DefaultGrants;

namespace AppointMe.Booking.Authorization;

public sealed class BookingDefaultGrantPolicy : IDefaultGrantPolicy
{
    public IReadOnlyCollection<RolePermissionGrant> DefaultGrants =>
    [
        new(Role.Owner,
            AppointmentPermissions.View,
            AppointmentPermissions.Schedule,
            AppointmentPermissions.Reschedule,
            AppointmentPermissions.Cancel,
            AppointmentPermissions.ViewStatistics
        ),
        new(Role.Manager,
            AppointmentPermissions.View,
            AppointmentPermissions.Schedule,
            AppointmentPermissions.Reschedule,
            AppointmentPermissions.Cancel,
            AppointmentPermissions.ViewStatistics
        ),
        new(Role.Staff,
            AppointmentPermissions.View,
            AppointmentPermissions.Schedule,
            AppointmentPermissions.Reschedule,
            AppointmentPermissions.Cancel
        ),
        new(Role.Receptionist,
            AppointmentPermissions.View,
            AppointmentPermissions.Schedule,
            AppointmentPermissions.Reschedule,
            AppointmentPermissions.Cancel
        )
    ];
}
