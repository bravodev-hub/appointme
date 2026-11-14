using AppointMe.Shared.Authorization.Permissions;

namespace AppointMe.Shared.Authorization.Principals;

public static class PrincipalAuthorizationExtensions
{
    public static void Require(this Principal principal, params Permission[] permissions)
    {
        if (permissions.Length == 0)
        {
            throw new ArgumentException("At least one permission must be specified.", nameof(permissions));
        }

        if (!permissions.All(principal.HasPermission))
        {
            throw new AccessDeniedException(
                $"The following permissions are required: {string.Join(", ", permissions.Select(permission => permission.Code))}.");
        }
    }
}
