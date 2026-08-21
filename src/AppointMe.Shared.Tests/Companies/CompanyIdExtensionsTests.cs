using AppointMe.Shared.Companies;

namespace AppointMe.Shared.Tests.Companies;

public class CompanyIdExtensionsTests
{
    [Fact]
    public void should_map_company_id_to_the_bare_guid_tenant_id()
    {
        var value = Guid.CreateVersion7();

        var tenantId = new CompanyId(value).ToTenantId();

        Assert.Equal(value.ToString(), tenantId);
    }

    [Fact]
    public void should_not_use_the_record_struct_default_formatting()
    {
        var companyId = new CompanyId(Guid.CreateVersion7());

        Assert.NotEqual(companyId.ToString(), companyId.ToTenantId());
    }
}
