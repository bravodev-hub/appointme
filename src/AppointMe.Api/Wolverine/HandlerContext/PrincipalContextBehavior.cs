namespace AppointMe.Api.Wolverine.HandlerContext;

public static class PrincipalContextBehavior
{
    public static async Task<Principal> LoadAsync(ICurrentPrincipalResolver principalResolver,
        CancellationToken cancellationToken)
    {
        return await principalResolver.Resolve(cancellationToken);
    }
}
