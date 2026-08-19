using AppointMe.Shared.Authorization.Permissions;

namespace AppointMe.Crm.Customers;

public static class CustomerPermissions
{
    private const string Customers = "customers";

    public static readonly Permission View = new(Customers, "view");
    public static readonly Permission Create = new(Customers, "create");
    public static readonly Permission Update = new(Customers, "update");
    public static readonly Permission Delete = new(Customers, "delete");
    public static readonly Permission ViewStatistics = new($"{Customers}.statistics", "view");
}
