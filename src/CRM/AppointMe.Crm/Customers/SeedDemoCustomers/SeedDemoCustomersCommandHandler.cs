using AppointMe.Crm.Database;
using Microsoft.Extensions.Logging;

namespace AppointMe.Crm.Customers.SeedDemoCustomers;

public sealed partial class SeedDemoCustomersCommandHandler(
    CrmDbContext dbContext,
    SeedDemoCustomers seeder,
    ILogger<SeedDemoCustomersCommandHandler> logger)
{
    public async Task HandleAsync(SeedDemoCustomersCommand command, CompanyId companyId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Customers.AnyAsync(cancellationToken))
        {
            return;
        }

        var customers = seeder.Generate(companyId, command.Count);

        dbContext.Customers.AddRange(customers);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogDemoCustomersSeeded(command.Count, companyId.Value);
    }

    [LoggerMessage(LogLevel.Information, "Seeded {Count} demo customers for company {CompanyId}.")]
    private partial void LogDemoCustomersSeeded(int count, Guid companyId);
}
