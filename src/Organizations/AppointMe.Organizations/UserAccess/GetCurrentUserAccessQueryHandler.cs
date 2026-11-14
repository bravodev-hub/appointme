namespace AppointMe.Organizations.UserAccess;

public sealed class GetCurrentUserAccessQueryHandler
{
    public GetCurrentUserAccessResponse Handle(GetCurrentUserAccessQuery query, Principal principal)
    {
        if (principal is not UserPrincipal userPrincipal)
        {
            throw new AccessDeniedException("User is not authenticated");
        }

        return new GetCurrentUserAccessResponse
        {
            CompanyId = userPrincipal.CompanyId.Value,
            Roles = userPrincipal.Roles.Select(role => role.Name).ToArray(),
            Permissions = userPrincipal.Permissions.Select(permission => permission.Code).ToArray()
        };
    }
}
