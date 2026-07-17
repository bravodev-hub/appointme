using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace AppointMe.Api.Authorization;

/// <summary>
/// Succeeds when the authenticated user's email is in the configured super-admin
/// allowlist. Reads the normalized <see cref="ClaimTypes.Email"/> claim produced
/// by the provider claims transformer, falling back to the raw <c>email</c> claim.
/// </summary>
public sealed class SuperAdminAuthorizationHandler(SuperAdminRegistry registry)
    : AuthorizationHandler<SuperAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SuperAdminRequirement requirement)
    {
        var email = context.User.FindFirstValue(ClaimTypes.Email)
                    ?? context.User.FindFirstValue("email");

        if (registry.IsSuperAdmin(email))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
