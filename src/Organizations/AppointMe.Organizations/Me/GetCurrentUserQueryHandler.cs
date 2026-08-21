using AppointMe.Organizations.Database;
using AppointMe.Organizations.Employees.Database;
using AppointMe.Organizations.Invitations;
using AppointMe.Organizations.Invitations.Database;

namespace AppointMe.Organizations.Me;

public sealed class GetCurrentUserQueryHandler(OrganizationsDbContext dbContext, SuperAdminRegistry superAdmins)
{
    public async Task<GetCurrentUserResponse> HandleAsync(GetCurrentUserQuery query, IIdentity identity,
        CancellationToken cancellationToken)
    {
        if (identity is not UserIdentity user)
        {
            return new GetCurrentUserResponse
            {
                IsAuthenticated = false,
            };
        }

        var companyMemberships = await dbContext
            .Employees.AsNoTracking()
            .IgnoreQueryFilters([EmployeeFilters.CompanyId])
            .Where(employee => employee.UserId == user.Id)
            .Join(dbContext.Companies,
                employee => employee.CompanyId,
                company => company.Id,
                (employee, company) => new CompanyMembership
                {
                    CompanyId = employee.CompanyId.Value,
                    CompanyName = company.Name.Value,
                    TimeZone = company.TimeZone.Id,
                })
            .ToArrayAsync(cancellationToken);

        var hasPendingInvitations = await dbContext
            .Invitations.AsNoTracking()
            .IgnoreQueryFilters([EmployeeInvitationFilters.CompanyId])
            .AnyAsync(invitation => invitation.Email == user.Email
                                    && invitation.Status == InvitationStatus.Pending, cancellationToken);

        return new GetCurrentUserResponse
        {
            IsAuthenticated = true,
            UserId = user.Id.Value,
            Name = user.Name.FullName,
            Initials = user.Name.Initials,
            Email = user.Email.Value,
            Companies = companyMemberships,
            HasPendingInvitations = hasPendingInvitations,
            HasMembership = companyMemberships.Length != 0,
            IsSuperAdmin = superAdmins.IsSuperAdmin(user.Email.Value),
        };
    }
}
