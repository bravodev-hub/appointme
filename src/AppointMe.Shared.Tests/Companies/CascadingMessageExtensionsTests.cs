using AppointMe.Shared.Companies;
using Wolverine;

namespace AppointMe.Shared.Tests.Companies;

public class CascadingMessageExtensionsTests
{
    private sealed record SomeCommand(int Count);

    [Fact]
    public void should_tag_the_message_with_the_company_tenant()
    {
        var companyId = new CompanyId(Guid.CreateVersion7());

        var tagged = new SomeCommand(3).WithCompany(companyId);

        Assert.Equal(companyId.ToTenantId(), tagged.Options.TenantId);
        Assert.Equal(new SomeCommand(3), tagged.Message);
    }

    [Fact]
    public void should_tag_an_already_configured_message_without_losing_its_options()
    {
        var companyId = new CompanyId(Guid.CreateVersion7());
        var scheduled = new SomeCommand(3).DelayedFor(TimeSpan.FromMinutes(5));

        var tagged = scheduled.WithCompany(companyId);

        Assert.Equal(companyId.ToTenantId(), tagged.Options.TenantId);
        Assert.Equal(TimeSpan.FromMinutes(5), tagged.Options.ScheduleDelay);
    }

    [Fact]
    public void should_tag_the_message_when_given_a_raw_company_guid()
    {
        var companyId = Guid.CreateVersion7();

        var tagged = new SomeCommand(3).WithCompany(companyId);

        Assert.Equal(companyId.ToString(), tagged.Options.TenantId);
        Assert.Equal(new SomeCommand(3), tagged.Message);
    }

    [Fact]
    public void should_tag_an_already_configured_message_when_given_a_raw_company_guid()
    {
        var companyId = Guid.CreateVersion7();
        var scheduled = new SomeCommand(3).DelayedFor(TimeSpan.FromMinutes(5));

        var tagged = scheduled.WithCompany(companyId);

        Assert.Equal(companyId.ToString(), tagged.Options.TenantId);
        Assert.Equal(TimeSpan.FromMinutes(5), tagged.Options.ScheduleDelay);
    }
}
