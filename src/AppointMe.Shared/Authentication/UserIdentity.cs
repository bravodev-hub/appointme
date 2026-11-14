using AppointMe.Shared.Domain.Common;
using AppointMe.Shared.Users;

namespace AppointMe.Shared.Authentication;

public sealed record UserIdentity(UserId Id, PersonName Name, Email Email);
