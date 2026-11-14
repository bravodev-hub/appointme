using AppointMe.Organizations.Database;
using AppointMe.Organizations.Employees.Database;

namespace AppointMe.Organizations.Employees.DeleteEmployee;

public sealed class DeleteEmployeeCommandHandler(OrganizationsDbContext dbContext)
{
    public async Task HandleAsync(DeleteEmployeeCommand command, CompanyId companyId, IPrincipal principal,
        Identity identity, CancellationToken cancellationToken)
    {
        principal.Require(EmployeePermissions.Remove);

        var employee = await dbContext.Employees.LoadAsync(new EmployeeId(command.Id), companyId, cancellationToken);
        if (identity is UserIdentity currentUser && employee.UserId == currentUser.Id)
        {
            throw new ValidationException("You cannot remove yourself from the company.");
        }

        employee.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
