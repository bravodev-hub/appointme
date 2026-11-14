namespace AppointMe.Shared.Authorization.Principals;

public interface ICurrentPrincipalResolver
{
    ValueTask<Principal> Resolve(CancellationToken cancellationToken);
}
