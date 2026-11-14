namespace AppointMe.Shared.Authentication;

public interface IIdentityResolver
{
    ValueTask<Identity> Resolve(CancellationToken cancellationToken);
}
