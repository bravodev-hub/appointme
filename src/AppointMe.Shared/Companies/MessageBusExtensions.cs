using Wolverine;

namespace AppointMe.Shared.Companies;

public static class MessageBusExtensions
{
    extension(IMessageBus bus)
    {
        public Task InvokeForCompany(CompanyId companyId, object message,
            CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        {
            return bus.InvokeForTenantAsync(companyId.ToTenantId(), message, cancellationToken, timeout);
        }

        public Task<T> InvokeForCompany<T>(CompanyId companyId, object message,
            CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        {
            return bus.InvokeForTenantAsync<T>(companyId.ToTenantId(), message, cancellationToken, timeout);
        }

        public Task InvokeForCompany(Guid companyId, object message,
            CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        {
            return bus.InvokeForCompany(new CompanyId(companyId), message, cancellationToken, timeout);
        }

        public Task<T> InvokeForCompany<T>(Guid companyId, object message,
            CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        {
            return bus.InvokeForCompany<T>(new CompanyId(companyId), message, cancellationToken, timeout);
        }
    }
}
