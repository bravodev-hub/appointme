using AppointMe.Booking.Appointments;
using AppointMe.Booking.ServiceProviders.Database;

namespace AppointMe.Booking.ServiceProviders.GetServiceProviders;

public sealed class GetServiceProvidersQueryHandler(ServiceProvidersRepository repository)
{
    public async Task<IReadOnlyList<ServiceProviderDto>> HandleAsync(GetServiceProvidersQuery query,
        CompanyId companyId, Principal principal, CancellationToken cancellationToken)
    {
        principal.Require(AppointmentPermissions.View);

        var providers = await repository.GetAll(companyId, cancellationToken);
        return providers.Select(provider => provider.Enrich()).ToList();
    }
}
