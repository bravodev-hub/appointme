using AppointMe.Crm.Database;

namespace AppointMe.Crm.Customers.TopUpDemoCustomers;

public sealed class TopUpDemoCustomersCommandHandler(
    CrmDbContext dbContext,
    SeedDemoCustomers.SeedDemoCustomers seeder,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(TopUpDemoCustomersCommand command, CompanyId companyId,
        CancellationToken cancellationToken)
    {
        var customers = seeder.Generate(companyId, command.Count, registrationDate: timeProvider.GetUtcNow());

        dbContext.Customers.AddRange(customers);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
