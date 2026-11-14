namespace AppointMe.Shared.Authentication;

public interface ICurrentIdentity
{
    Identity? Identity { get; }
    IDisposable Change(Identity identity);
}
