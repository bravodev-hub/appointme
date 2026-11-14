using AppointMe.Organizations.Database;
using AppointMe.Organizations.Invitations.Database;

namespace AppointMe.Organizations.Invitations.CancelInvitation;

public sealed class CancelInvitationCommandHandler(OrganizationsDbContext dbContext)
{
    public async Task HandleAsync(CancelInvitationCommand command, CompanyId companyId, Principal principal,
        CancellationToken cancellationToken)
    {
        principal.Require(InvitationPermissions.Cancel);

        var invitation =
            await dbContext.Invitations.LoadAsync(new EmployeeInvitationId(command.Id), companyId, cancellationToken);

        invitation.Cancel();

        dbContext.Invitations.Remove(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
