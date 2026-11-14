using AppointMe.Organizations.Database;
using AppointMe.Organizations.Invitations.Database;

namespace AppointMe.Organizations.Invitations.GetPendingInvitations;

public sealed class GetPendingInvitationsQueryHandler(OrganizationsDbContext dbContext)
{
    public async Task<GetPendingInvitationsResponse> HandleAsync(GetPendingInvitationsQuery query, Identity identity,
        CancellationToken cancellationToken)
    {
        if (identity is not UserIdentity user)
        {
            return new GetPendingInvitationsResponse
            {
                Invitations = []
            };
        }

        var invitations = await dbContext.Invitations
            .AsNoTracking()
            .IgnoreQueryFilters([EmployeeInvitationFilters.CompanyId])
            .Where(invitation => invitation.Email == user.Email
                                 && invitation.Status == InvitationStatus.Pending)
            .Join(dbContext.Companies,
                invitation => invitation.CompanyId,
                company => company.Id,
                (invitation, company) => new PendingInvitationDto
                {
                    Id = invitation.Id.Value,
                    CompanyId = invitation.CompanyId.Value,
                    CompanyName = company.Name.Value,
                    Roles = invitation.Roles,
                    ExpiresAt = invitation.ExpiresAt,
                })
            .ToListAsync(cancellationToken);

        return new GetPendingInvitationsResponse
        {
            Invitations = invitations
        };
    }
}
