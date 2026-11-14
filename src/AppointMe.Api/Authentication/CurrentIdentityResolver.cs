namespace AppointMe.Api.Authentication;

using Identity = AppointMe.Shared.Authentication.Identity;

public sealed class CurrentIdentityResolver(
    ICurrentIdentity currentIdentity,
    HttpIdentityFactory httpIdentityFactory
) : IIdentityResolver
{
    public async ValueTask<Identity> Resolve(CancellationToken cancellationToken)
    {
        if (currentIdentity.Identity is { } resolved)
        {
            return resolved;
        }

        var identity = await httpIdentityFactory.Create(cancellationToken);
        currentIdentity.Change(identity);
        return identity;
    }
}
