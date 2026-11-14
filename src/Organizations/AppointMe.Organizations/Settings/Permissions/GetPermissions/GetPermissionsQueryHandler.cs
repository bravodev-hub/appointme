    using AppointMe.Organizations.Database;
using AppointMe.Organizations.Settings.Permissions.Database;
using AppointMe.Shared.Authorization.Permissions;

namespace AppointMe.Organizations.Settings.Permissions.GetPermissions;

public sealed class GetPermissionsQueryHandler(PermissionRegistry registry, OrganizationsDbContext dbContext)
{
    public async Task<GetPermissionsResponse> HandleAsync(GetPermissionsQuery query, Principal principal,
        CompanyId companyId, CancellationToken cancellationToken)
    {
        principal.Require(PermissionPermissions.View);

        var overrideMap = await dbContext.RolePermissionOverrides
            .AsNoTracking()
            .LoadAsync(companyId, cancellationToken);

        var permissionGroups = registry.Permissions
            .Where(permission => permission is not SystemPermission)
            .GroupBy(permission => permission.Resource)
            .OrderBy(permissionGroup => permissionGroup.Key, StringComparer.Ordinal)
            .Select(permissionGroup => new PermissionGroupDto
            {
                Name = permissionGroup.Key,
                Permissions = permissionGroup
                    .OrderBy(permission => permission.Action, StringComparer.Ordinal)
                    .Select(permission => new PermissionDto
                    {
                        Key = permission.Code,
                        Grants = Role.Configurable.ToDictionary(
                            role => role,
                            role =>
                            {
                                var hasOverride =
                                    overrideMap.TryGetValue((permission.Code, role), out var overrideValue);
                                return new PermissionGrantDto
                                {
                                    IsGranted = hasOverride
                                        ? overrideValue is { IsGranted: true }
                                        : registry.IsGrantedByDefault(role, permission.Code),
                                    IsOverridden = hasOverride,
                                };
                            })
                    }).ToArray()
            });

        return new GetPermissionsResponse
        {
            Roles = Role.BuiltIn.Select(role => new RoleDefinitionDto
            {
                Role = role,
                AllowsPermissionOverrides = role is not SystemRole
            }).ToArray(),
            Groups = permissionGroups.ToArray()
        };
    }
}
