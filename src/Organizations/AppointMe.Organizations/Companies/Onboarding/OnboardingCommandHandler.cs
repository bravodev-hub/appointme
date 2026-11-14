using AppointMe.Organizations.Database;

namespace AppointMe.Organizations.Companies.Onboarding;

public sealed class OnboardingCommandHandler(OrganizationsDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<OnboardingResponse> HandleAsync(OnboardingCommand command, Identity identity,
        CancellationToken cancellationToken)
    {
        if (identity is not UserIdentity currentUser)
        {
            throw new AccessDeniedException("Only authenticated users can onboard a company");
        }

        var (company, owner) = Company.Onboard(
            name: CompanyName.Create(command.Name),
            timeZone: TimeZoneInfo.Create(command.TimeZone),
            ownerName: currentUser.Name,
            ownerEmail: currentUser.Email,
            registeredBy: currentUser.Id,
            registrationDate: timeProvider.GetUtcNow()
        );

        await dbContext.Companies.AddAsync(company, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Employees.AddAsync(owner, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        company.AssignPrimaryOwner(owner.Id);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new OnboardingResponse(company.Id.Value);
    }
}
