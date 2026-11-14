using AppointMe.Shared.Users;

namespace AppointMe.Shared.Authentication;

public static class IdentityExtensions
{
    extension(Identity identity)
    {
        public UserId? UserId => identity is UserIdentity user ? user.Id : null;
    }
}
