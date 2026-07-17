using System.Security.Claims;
using AppointMe.Api.Authorization;
using AppointMe.Shared.Authentication;
using AppointMe.Shared.Domain.Common;
using AppointMe.Shared.Users;
using Microsoft.AspNetCore.Authorization;

namespace AppointMe.Api.Tests.Authorization;

public class SuperAdminAuthorizationHandlerTests
{
    private static readonly SuperAdminRegistry Registry = new(["demo@appointme.dev"]);

    private static async Task<bool> Evaluate(IIdentity identity)
    {
        var handler = new SuperAdminAuthorizationHandler(new StubIdentityResolver(identity), Registry);
        var requirement = new SuperAdminRequirement();
        var user = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    private static UserIdentity UserWithEmail(string email)
    {
        return new UserIdentity(
            new UserId(Guid.NewGuid()),
            PersonName.Create("Demo", "Admin"),
            Email.Create(email));
    }

    [Fact]
    public async Task should_succeed_when_user_email_is_super_admin()
    {
        Assert.True(await Evaluate(UserWithEmail("demo@appointme.dev")));
    }

    [Fact]
    public async Task should_not_succeed_when_user_email_is_not_super_admin()
    {
        Assert.False(await Evaluate(UserWithEmail("someone-else@appointme.dev")));
    }

    [Fact]
    public async Task should_not_succeed_for_anonymous_identity()
    {
        Assert.False(await Evaluate(new AnonymousIdentity()));
    }

    [Fact]
    public async Task should_not_succeed_for_system_identity()
    {
        Assert.False(await Evaluate(new SystemIdentity()));
    }

    private sealed class StubIdentityResolver(IIdentity identity) : IIdentityResolver
    {
        public ValueTask<IIdentity> Resolve(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(identity);
        }
    }
}
