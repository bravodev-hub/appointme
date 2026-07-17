using Microsoft.AspNetCore.Authorization;

namespace AppointMe.Api.Authorization;

/// <summary>
/// Requires the current user to be a platform super admin. Enforced as an
/// authorization requirement (mirroring <see cref="RegisteredUserRequirement"/>)
/// rather than a claims transformation, so it does not interfere with the single
/// provider <c>IClaimsTransformation</c> that normalizes identity claims.
/// </summary>
public sealed record SuperAdminRequirement : IAuthorizationRequirement;
