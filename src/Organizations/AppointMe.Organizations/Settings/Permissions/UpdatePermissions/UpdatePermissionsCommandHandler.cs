using AppointMe.Organizations.Contracts.Settings.Permissions.Events;
using AppointMe.Organizations.Database;
using AppointMe.Organizations.Settings.Permissions.Database;
using AppointMe.Shared.Authorization.Permissions;

namespace AppointMe.Organizations.Settings.Permissions.UpdatePermissions;

public sealed class UpdatePermissionsCommandHandler(PermissionRegistry registry, OrganizationsDbContext dbContext)
{
    public async Task<CompanyPermissionsChanged?> HandleAsync(UpdatePermissionsCommand command, CompanyId companyId,
        IPrincipal principal, Identity identity, CancellationToken cancellationToken)
    {
        principal.Require(PermissionPermissions.Manage);

        if (command.Grants.Count == 0)
        {
            return null;
        }

        var existingOverrides = await dbContext.RolePermissionOverrides.LoadAsync(companyId, cancellationToken);
        var policy = CompanyPermissionPolicy.Load(companyId, existingOverrides);

        var permissionsChanged = policy.ApplyGrants(command.Grants, registry, identity.UserId);

        dbContext.RolePermissionOverrides.AddRange(policy.Added);
        dbContext.RolePermissionOverrides.RemoveRange(policy.Removed);
        await dbContext.SaveChangesAsync(cancellationToken);

        return permissionsChanged;
    }
}
