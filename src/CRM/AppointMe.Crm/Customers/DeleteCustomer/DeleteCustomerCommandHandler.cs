using AppointMe.Crm.Customers.Database;
using AppointMe.Crm.Database;

namespace AppointMe.Crm.Customers.DeleteCustomer;

public sealed class DeleteCustomerCommandHandler(CrmDbContext dbContext)
{
    public async Task HandleAsync(DeleteCustomerCommand command, Principal principal,
        CancellationToken cancellationToken)
    {
        principal.Require(CustomerPermissions.Delete);

        var customer = await dbContext.Customers.LoadAsync(new CustomerId(command.CustomerId), cancellationToken);

        customer.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
