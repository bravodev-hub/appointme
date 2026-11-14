using AppointMe.Crm.Customers.Database;
using AppointMe.Crm.Database;

namespace AppointMe.Crm.Customers.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler(CrmDbContext dbContext, TimeProvider timeProvider)
{
    public async Task HandleAsync(UpdateCustomerCommand command, Principal principal,
        CancellationToken cancellationToken)
    {
        principal.Require(CustomerPermissions.Update);

        var customer = await dbContext.Customers.LoadAsync(new CustomerId(command.CustomerId), cancellationToken);

        customer.Update(
            name: PersonName.Create(command.FirstName, command.LastName),
            dateOfBirth: DateOfBirth.CreateOrNull(command.DateOfBirth, timeProvider),
            gender: Gender.ParseOrNull(command.Gender),
            email: Email.CreateOrNull(command.Email)
        );

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
