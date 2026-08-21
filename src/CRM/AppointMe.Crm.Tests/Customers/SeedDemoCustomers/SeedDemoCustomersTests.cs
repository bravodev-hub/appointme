using System.Globalization;
using AppointMe.Shared.Companies;
using Microsoft.Extensions.Time.Testing;

namespace AppointMe.Crm.Tests.Customers.SeedDemoCustomers;

public class SeedDemoCustomersTests
{
    [Fact]
    public void should_stamp_customers_with_provided_registration_date()
    {
        var timeProvider = new FakeTimeProvider(
            DateTimeOffset.Parse("19 Aug 2026 03:00Z", DateTimeFormatInfo.InvariantInfo));
        var seeder = new Crm.Customers.SeedDemoCustomers.SeedDemoCustomers(timeProvider);
        var registrationDate = timeProvider.GetUtcNow();

        var customers = seeder.Generate(new CompanyId(NewId()), count: 5, registrationDate).ToList();

        Assert.Equal(5, customers.Count);
        Assert.All(customers, customer => Assert.Equal(registrationDate, customer.RegistrationDate));
    }
}
