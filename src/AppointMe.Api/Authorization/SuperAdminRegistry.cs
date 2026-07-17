namespace AppointMe.Api.Authorization;

/// <summary>
/// Holds the configured set of super-admin identities (by email). Membership is
/// sourced from the <see cref="SuperAdmin.ConfigurationSection"/> config list so
/// it works uniformly across identity providers. Swap this out for an IdP realm
/// role / app role lookup if you prefer assignment to live in the IdP.
/// </summary>
public sealed class SuperAdminRegistry(IEnumerable<string> emails)
{
    private readonly HashSet<string> _emails = new(emails, StringComparer.OrdinalIgnoreCase);

    public bool IsSuperAdmin(string? email) => email is not null && _emails.Contains(email);
}
