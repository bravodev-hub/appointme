namespace AppointMe.Shared.Authorization.Principals;

public interface ICurrentPrincipal
{
    Principal? Principal { get; }
    IDisposable Change(Principal principal);
}
