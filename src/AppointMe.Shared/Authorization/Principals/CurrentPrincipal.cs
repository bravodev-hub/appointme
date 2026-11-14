namespace AppointMe.Shared.Authorization.Principals;

public sealed class CurrentPrincipal : ICurrentPrincipal
{
    private static readonly AsyncLocal<Principal?> Current = new();

    public Principal? Principal
    {
        get => Current.Value;
        private set => Current.Value = value;
    }

    public IDisposable Change(Principal principal)
    {
        var previous = Principal;
        Principal = principal;
        return new Scope(() => Principal = previous);
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
