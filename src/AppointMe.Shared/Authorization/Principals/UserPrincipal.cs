using AppointMe.Shared.Authorization.Roles;
using AppointMe.Shared.Companies;
using AppointMe.Shared.Authorization.Permissions;

namespace AppointMe.Shared.Authorization.Principals;

public sealed record UserPrincipal(CompanyId CompanyId, IReadOnlySet<Role> Roles, IReadOnlySet<Permission> Permissions)
{
    public bool HasRole(Role role) => Roles.Contains(role);
    public bool HasPermission(Permission permission) => Permissions.Contains(permission);
}
