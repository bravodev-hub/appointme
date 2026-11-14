namespace AppointMe.Organizations.Infrastructure;

public sealed class CurrentPrincipalResolver(
    IIdentityResolver identityResolver,
    ICurrentPrincipal currentPrincipal,
    UserPrincipalFactory userPrincipalFactory
) : ICurrentPrincipalResolver
{
    public async ValueTask<Principal> Resolve(CancellationToken cancellationToken)
    {
        if (currentPrincipal.Principal is { } resolved)
        {
            return resolved;
        }

        var identity = await identityResolver.Resolve(cancellationToken);

        Principal principal = identity switch
        {
            AnonymousIdentity => new AnonymousPrincipal(),
            SystemIdentity => new SystemPrincipal(),
            UserIdentity user => await userPrincipalFactory.Create(user, cancellationToken),
        };

        currentPrincipal.Change(principal);
        return principal;
    }
}
