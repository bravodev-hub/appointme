using AppointMe.Shared.Authorization.Roles;
using AppointMe.Shared.Authorization.Permissions;

namespace AppointMe.Shared.Authorization.Principals;

public union Principal(AnonymousPrincipal, SystemPrincipal, UserPrincipal)
{
    public bool HasRole(Role role) => this switch
    {
        SystemPrincipal => true,
        UserPrincipal user => user.HasRole(role),
        _ => false,
    };

    public bool HasPermission(Permission permission) => this switch
    {
        SystemPrincipal => true,
        UserPrincipal user => user.HasPermission(permission),
        _ => false,
    };
}
