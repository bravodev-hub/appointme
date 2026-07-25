using AppointMe.Organizations.Companies;
using AppointMe.Organizations.Companies.Database;
using AppointMe.Organizations.Database;
using AppointMe.Organizations.Employees.Database;

namespace AppointMe.Organizations.Employees.UpdateEmployeeRoles;

public sealed class UpdateEmployeeRolesCommandHandler(OrganizationsDbContext dbContext)
{
    public async Task HandleAsync(UpdateEmployeeRolesCommand command, CompanyId companyId, IPrincipal principal,
        CancellationToken cancellationToken)
    {
        principal.Require(EmployeePermissions.UpdateRoles);

        var employee = await dbContext.Employees.LoadAsync(new EmployeeId(command.Id), companyId, cancellationToken);
        var company = await dbContext.Companies.LoadAsync(companyId, cancellationToken);

        employee.UpdateRoles(
            command.Roles.ToHashSet(),
            company.LockedRolesFor(employee.Id).ToHashSet(),
            canManageSystemRoles: principal.HasPermission(EmployeePermissions.ManageOwners));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
