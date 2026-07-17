using System.Security.Claims;
using AppointMe.Api.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AppointMe.Api.Tests.Authorization;

public class SuperAdminAuthorizationHandlerTests
{
    private static readonly SuperAdminRegistry Registry = new(["demo@appointme.dev"]);

    private static async Task<bool> Evaluate(params Claim[] claims)
    {
        var handler = new SuperAdminAuthorizationHandler(Registry);
        var requirement = new SuperAdminRequirement();
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    [Fact]
    public async Task should_succeed_when_normalized_email_is_super_admin()
    {
        Assert.True(await Evaluate(new Claim(ClaimTypes.Email, "demo@appointme.dev")));
    }

    [Fact]
    public async Task should_succeed_when_only_raw_email_claim_present()
    {
        Assert.True(await Evaluate(new Claim("email", "demo@appointme.dev")));
    }

    [Fact]
    public async Task should_not_succeed_when_email_is_not_super_admin()
    {
        Assert.False(await Evaluate(new Claim(ClaimTypes.Email, "someone-else@appointme.dev")));
    }

    [Fact]
    public async Task should_not_succeed_when_no_email_claim_present()
    {
        Assert.False(await Evaluate(new Claim(ClaimTypes.NameIdentifier, "user-id-only")));
    }
}
