using AppointMe.Organizations.Database;
using AppointMe.Organizations.Invitations.Database;

namespace AppointMe.Organizations.Invitations.ResendInvitation;

public sealed class ResendInvitationCommandHandler(OrganizationsDbContext dbContext, TimeProvider timeProvider)
{
    public async Task HandleAsync(ResendInvitationCommand command, CompanyId companyId, Principal principal,
        CancellationToken cancellationToken)
    {
        principal.Require(InvitationPermissions.Resend);

        var invitation =
            await dbContext.Invitations.LoadAsync(new EmployeeInvitationId(command.Id), companyId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        invitation.Resend(now);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
