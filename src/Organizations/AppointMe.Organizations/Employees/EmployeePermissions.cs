using AppointMe.Shared.Authorization.Permissions;

namespace AppointMe.Organizations.Employees;

public static class EmployeePermissions
{
    private const string Employees = "employees";

    public static readonly Permission View = new(Employees, "view");
    public static readonly Permission Invite = new(Employees, "invite");
    public static readonly Permission Remove = new(Employees, "remove");
    public static readonly Permission UpdateRoles = new(Employees, "update_roles");
    public static readonly SystemPermission ManageOwners = new(Employees, "manage_owners");
}
