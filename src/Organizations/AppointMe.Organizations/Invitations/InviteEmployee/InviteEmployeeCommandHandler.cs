using AppointMe.Organizations.Database;
using AppointMe.Organizations.Employees;

namespace AppointMe.Organizations.Invitations.InviteEmployee;

public sealed class InviteEmployeeCommandHandler(OrganizationsDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<InviteEmployeeResponse> HandleAsync(InviteEmployeeCommand command, CompanyId companyId,
        IPrincipal principal, IIdentity identity, CancellationToken cancellationToken)
    {
        principal.Require(EmployeePermissions.Invite);

        if (identity is not UserIdentity currentUser)
        {
            throw new AccessDeniedException("Only authenticated users can invite employees to a company.");
        }

        var email = Email.Create(command.Email);

        var isExistingEmployee = await dbContext.Employees
            .AnyAsync(employee => employee.CompanyId == companyId && employee.Email == email, cancellationToken);

        if (isExistingEmployee)
        {
            throw new ConflictException("An employee with this email already exists in this company.",
                "employee_already_exists");
        }

        var hasPendingInvitation = await dbContext.Invitations
            .AnyAsync(invitation => invitation.CompanyId == companyId
                                    && invitation.Email == email
                                    && invitation.Status == InvitationStatus.Pending, cancellationToken);

        if (hasPendingInvitation)
        {
            throw new ConflictException("A pending invitation already exists for this email.",
                code: "invitation_already_exists");
        }

        var now = timeProvider.GetUtcNow();

        var invitation = EmployeeInvitation.Create(
            companyId: companyId,
            email: email,
            roles: command.Roles,
            invitedBy: currentUser.Id,
            now: now,
            canManageSystemRoles: principal.HasPermission(EmployeePermissions.ManageOwners)
        );

        await dbContext.Invitations.AddAsync(invitation, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new InviteEmployeeResponse(invitation.Id.Value);
    }
}
