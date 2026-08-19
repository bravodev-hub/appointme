using AppointMe.Crm.Customers;
using AppointMe.Shared.Authorization.Roles;
using AppointMe.Shared.Authorization.Permissions.DefaultGrants;

namespace AppointMe.Crm.Authorization;

public sealed class CrmDefaultGrantPolicy : IDefaultGrantPolicy
{
    public IReadOnlyCollection<RolePermissionGrant> DefaultGrants =>
    [
        new(Role.Owner,
            CustomerPermissions.View,
            CustomerPermissions.Create,
            CustomerPermissions.Update,
            CustomerPermissions.Delete,
            CustomerPermissions.ViewStatistics
        ),
        new(Role.Manager,
            CustomerPermissions.View,
            CustomerPermissions.Create,
            CustomerPermissions.Update,
            CustomerPermissions.ViewStatistics
        ),
        new(Role.Staff,
            CustomerPermissions.View
        ),
        new(Role.Receptionist,
            CustomerPermissions.View,
            CustomerPermissions.Create,
            CustomerPermissions.Update
        )
    ];
}
