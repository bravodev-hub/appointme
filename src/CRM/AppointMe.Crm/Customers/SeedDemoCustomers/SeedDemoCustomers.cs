using AppointMe.Crm.Customers.RegisterCustomer;
using Bogus;

namespace AppointMe.Crm.Customers.SeedDemoCustomers;

public sealed class SeedDemoCustomers(TimeProvider timeProvider)
{
    public IEnumerable<Customer> Generate(CompanyId companyId, int count, DateTimeOffset? registrationDate = null)
    {
        var faker = new Faker();
        return Enumerable.Range(0, count)
            .Select(_ => new Person())
            .Select(person => Customer.Register(
                companyId: companyId,
                name: PersonName.Create(person.FirstName, person.LastName),
                dateOfBirth: DateOfBirth.Create(DateOnly.FromDateTime(person.DateOfBirth), timeProvider),
                gender: person.Gender == Bogus.DataSets.Name.Gender.Female ? Gender.Female : Gender.Male,
                email: Email.Create(person.Email),
                registrationDate: registrationDate ?? faker.Date.PastOffset()));
    }
}
