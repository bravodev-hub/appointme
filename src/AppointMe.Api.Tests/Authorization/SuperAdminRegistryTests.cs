using AppointMe.Api.Authorization;

namespace AppointMe.Api.Tests.Authorization;

public class SuperAdminRegistryTests
{
    [Fact]
    public void should_recognize_configured_super_admin_email()
    {
        var registry = new SuperAdminRegistry(["demo@appointme.dev"]);

        Assert.True(registry.IsSuperAdmin("demo@appointme.dev"));
    }

    [Fact]
    public void should_match_email_case_insensitively()
    {
        var registry = new SuperAdminRegistry(["demo@appointme.dev"]);

        Assert.True(registry.IsSuperAdmin("Demo@AppointMe.DEV"));
    }

    [Fact]
    public void should_reject_email_not_in_allowlist()
    {
        var registry = new SuperAdminRegistry(["demo@appointme.dev"]);

        Assert.False(registry.IsSuperAdmin("someone-else@appointme.dev"));
    }

    [Fact]
    public void should_reject_null_email()
    {
        var registry = new SuperAdminRegistry(["demo@appointme.dev"]);

        Assert.False(registry.IsSuperAdmin(null));
    }

    [Fact]
    public void should_deny_everyone_when_allowlist_is_empty()
    {
        var registry = new SuperAdminRegistry([]);

        Assert.False(registry.IsSuperAdmin("demo@appointme.dev"));
    }
}
