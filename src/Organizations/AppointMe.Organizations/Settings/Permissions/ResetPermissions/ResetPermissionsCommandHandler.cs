using AppointMe.Organizations.Contracts.Settings.Permissions.Events;
using AppointMe.Organizations.Database;
using AppointMe.Organizations.Settings.Permissions.Database;

namespace AppointMe.Organizations.Settings.Permissions.ResetPermissions;

public sealed class ResetPermissionsCommandHandler(OrganizationsDbContext dbContext)
{
    public async Task<CompanyPermissionsChanged?> HandleAsync(ResetPermissionsCommand command, CompanyId companyId,
        IPrincipal principal, Identity identity, CancellationToken cancellationToken)
    {
        principal.Require(PermissionPermissions.Manage);

        var existingOverrides = await dbContext.RolePermissionOverrides.LoadAsync(companyId, cancellationToken);
        var policy = CompanyPermissionPolicy.Load(companyId, existingOverrides);

        var permissionsChanged = policy.Reset(identity.UserId);

        dbContext.RolePermissionOverrides.RemoveRange(policy.Removed);
        await dbContext.SaveChangesAsync(cancellationToken);

        return permissionsChanged;
    }
}
