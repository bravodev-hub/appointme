using AppointMe.Organizations.Contracts.Companies;

namespace AppointMe.Crm.Customers.TopUpDemoCustomers;

public sealed class TopUpDemoCustomersJob(ICompanyRehydrationSource companySource, IMessageBus bus)
{
    private const int CustomersPerCompany = 6;

    public async Task Run(CancellationToken cancellationToken)
    {
        var companies = await companySource.GetAll(cancellationToken);
        foreach (var company in companies)
        {
            await bus.InvokeForCompany(company.CompanyId, new TopUpDemoCustomersCommand(CustomersPerCompany),
                cancellationToken);
        }
    }
}
