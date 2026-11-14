using AppointMe.Organizations.Companies;
using AppointMe.Organizations.Companies.Database;
using AppointMe.Organizations.Database;
using AppointMe.Organizations.Employees.Database;
using AppointMe.Shared.Pagination;
using AppointMe.Shared.Utilities;

namespace AppointMe.Organizations.Employees.GetTeam;

public sealed class GetTeamQueryHandler(TeamRepository repository, OrganizationsDbContext dbContext)
{
    public async Task<GetTeamResponse> HandleAsync(GetTeamQuery query, CompanyId companyId, Principal principal,
        Identity identity, CancellationToken cancellationToken)
    {
        principal.Require(EmployeePermissions.View);

        var members = await repository.GetTeam(new GetTeamFilter
        {
            SearchTokens = query.Search.Tokenize(),
            Pagination = query.Pagination,
        }, companyId, cancellationToken);

        var company = await dbContext.Companies.LoadAsync(companyId, cancellationToken: cancellationToken);
        var enrichedMembers = members.Items.Select(member =>
        {
            var personName = PersonName.CreateOrNull(member.FirstName, member.LastName);
            var employeeId = new EmployeeId(member.Id);
            return member with
            {
                FullName = personName?.FullName,
                Initials = personName?.Initials,
                IsCurrentUser = identity is UserIdentity user && user.Id.Value == member.UserId,
                IsPrimaryOwner = company.IsPrimaryOwner(employeeId),
                LockedRoles = company.LockedRolesFor(employeeId).ToArray()
            };
        });

        return new GetTeamResponse
        {
            Members = enrichedMembers.ToPagedResult(query.Pagination, members.TotalCount)
        };
    }
}
