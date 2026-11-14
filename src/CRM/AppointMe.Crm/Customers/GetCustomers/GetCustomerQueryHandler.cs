using AppointMe.Crm.Customers.Database;
using AppointMe.Shared.Utilities;

namespace AppointMe.Crm.Customers.GetCustomers;

public sealed class GetCustomerQueryHandler(CustomersRepository repository)
{
    public async Task<GetCustomersResponse> HandleAsync(GetCustomerQuery query, CompanyId companyId,
        Principal principal, CancellationToken cancellationToken)
    {
        principal.Require(CustomerPermissions.View);

        var customers =
            await repository.GetAll(query.Search.Tokenize(), query.Pagination, companyId, cancellationToken);
        return new GetCustomersResponse
        {
            Customers = customers
        };
    }
}
