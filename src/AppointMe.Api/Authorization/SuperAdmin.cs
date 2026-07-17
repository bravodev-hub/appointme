namespace AppointMe.Api.Authorization;

/// <summary>
/// Platform-level "super admin" role. Unlike the per-company business roles
/// (Owner/Manager/Staff/…), this is a cross-tenant, identity-level role: it is
/// not tied to an active company and grants access to platform-operator
/// surfaces such as the Hangfire dashboard.
/// </summary>
public static class SuperAdmin
{
    /// <summary>Configuration section holding the list of super-admin emails.</summary>
    public const string ConfigurationSection = "Authentication:SuperAdmins";
}
