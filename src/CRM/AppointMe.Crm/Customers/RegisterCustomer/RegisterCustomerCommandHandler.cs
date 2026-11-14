using AppointMe.Crm.Database;

namespace AppointMe.Crm.Customers.RegisterCustomer;

public sealed class RegisterCustomerCommandHandler(
    CrmDbContext dbContext,
    TimeProvider timeProvider
)
{
    public async Task<RegisterCustomerResponse> HandleAsync(RegisterCustomerCommand command, CompanyId companyId,
        Principal principal, CancellationToken cancellationToken)
    {
        principal.Require(CustomerPermissions.Create);

        var customer = Customer.Register(
            companyId: companyId,
            name: PersonName.Create(command.FirstName, command.LastName),
            dateOfBirth: DateOfBirth.CreateOrNull(command.DateOfBirth, timeProvider),
            gender: Gender.ParseOrNull(command.Gender),
            email: Email.CreateOrNull(command.Email),
            registrationDate: timeProvider.GetUtcNow()
        );

        await dbContext.Customers.AddAsync(customer, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterCustomerResponse(customer.Id.Value);
    }
}
