using AppointMe.Crm.Customers.Database;

namespace AppointMe.Crm.Customers.GetCustomerById;

public sealed class GetCustomerByIdQueryHandler(CustomersRepository repository)
{
    public async Task<CustomerDto> HandleAsync(GetCustomerByIdQuery query, CompanyId companyId,
        Principal principal, CancellationToken cancellationToken)
    {
        principal.Require(CustomerPermissions.View);

        return await repository.LoadById(new CustomerId(query.CustomerId), companyId, cancellationToken);
    }
}
