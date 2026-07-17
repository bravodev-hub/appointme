namespace AppointMe.Api.Authorization;

/// <summary>
/// Authorization policy guarding the Hangfire dashboard. Requires an
/// authenticated user who satisfies <see cref="SuperAdminRequirement"/>, so the
/// cross-tenant ops dashboard is reachable only by platform super admins.
/// </summary>
public static class HangfireDashboardPolicy
{
    public const string Name = "HangfireDashboard";
}
