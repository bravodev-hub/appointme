using AppointMe.Shared.Authorization.Permissions.DefaultGrants;

namespace AppointMe.Organizations.Employees;

public static class EmployeeRolesGrants
{
    public static readonly IReadOnlyCollection<RolePermissionGrant> DefaultGrants =
    [
        new(Role.Owner,
            EmployeePermissions.View,
            EmployeePermissions.Invite,
            EmployeePermissions.Remove,
            EmployeePermissions.UpdateRoles,
            EmployeePermissions.ManageOwners
        ),
        new(Role.Manager,
            EmployeePermissions.View,
            EmployeePermissions.Invite,
            EmployeePermissions.Remove,
            EmployeePermissions.UpdateRoles
        )
    ];
}
