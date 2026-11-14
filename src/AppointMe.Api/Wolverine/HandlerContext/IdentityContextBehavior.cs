namespace AppointMe.Api.Wolverine.HandlerContext;

using Identity = AppointMe.Shared.Authentication.Identity;

public static class IdentityContextBehavior
{
    public static async Task<Identity> Load(IIdentityResolver identityResolver,
        CancellationToken cancellationToken)
    {
        return await identityResolver.Resolve(cancellationToken);
    }
}
