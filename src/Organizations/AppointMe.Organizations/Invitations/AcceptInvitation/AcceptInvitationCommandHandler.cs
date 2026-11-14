using AppointMe.Organizations.Database;
using AppointMe.Organizations.Employees;
using AppointMe.Organizations.Employees.RegisterEmployee;
using AppointMe.Organizations.Invitations.Database;

namespace AppointMe.Organizations.Invitations.AcceptInvitation;

public sealed class AcceptInvitationCommandHandler(OrganizationsDbContext dbContext, TimeProvider timeProvider)
{
    public async Task HandleAsync(AcceptInvitationCommand command, Identity identity,
        CancellationToken cancellationToken)
    {
        if (identity is not UserIdentity currentUser)
        {
            throw new AccessDeniedException("Only authenticated users can accept invitations.");
        }

        var invitation = await dbContext.Invitations.LoadForRecipientAsync(new EmployeeInvitationId(command.Id),
            currentUser.Email, cancellationToken);

        var now = timeProvider.GetUtcNow();

        invitation.Accept(currentUser.Id, now);

        var employee = Employee.Register(
            companyId: invitation.CompanyId,
            name: currentUser.Name,
            email: invitation.Email,
            roles: invitation.Roles,
            userId: currentUser.Id,
            registrationDate: now
        );

        await dbContext.Employees.AddAsync(employee, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
