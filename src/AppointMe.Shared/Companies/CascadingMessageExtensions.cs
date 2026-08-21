using Wolverine;

namespace AppointMe.Shared.Companies;

public static class CascadingMessageExtensions
{
    public static DeliveryMessage<T> WithCompany<T>(this T message, CompanyId companyId)
    {
        return message.WithTenantId(companyId.ToTenantId());
    }

    public static DeliveryMessage<T> WithCompany<T>(this DeliveryMessage<T> message, CompanyId companyId)
    {
        return message.WithTenantId(companyId.ToTenantId());
    }

    public static DeliveryMessage<T> WithCompany<T>(this T message, Guid companyId)
    {
        return message.WithCompany(new CompanyId(companyId));
    }

    public static DeliveryMessage<T> WithCompany<T>(this DeliveryMessage<T> message, Guid companyId)
    {
        return message.WithCompany(new CompanyId(companyId));
    }
}
