using Microsoft.AspNetCore.Authorization;

namespace AppointMe.Api.Authorization;

public sealed class SuperAdminAuthorizationHandler(
    IIdentityResolver identityResolver,
    SuperAdminRegistry registry
) : AuthorizationHandler<SuperAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SuperAdminRequirement requirement)
    {
        var identity = await identityResolver.Resolve(CancellationToken.None);

        if (identity is UserIdentity user && registry.IsSuperAdmin(user.Email.Value))
        {
            context.Succeed(requirement);
        }
    }
}
