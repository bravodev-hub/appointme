using AppointMe.Organizations.Contracts.Employees.Events;
using AppointMe.Organizations.Employees;
using AppointMe.Organizations.Employees.UpdateEmployeeRoles;
using AppointMe.Shared.Domain.Common;
using AppointMe.Shared.Domain.Errors;
using AppointMe.Shared.Users;

namespace AppointMe.Organizations.Tests.Employees;

public class UpdateEmployeeRolesTests
{
    private static Employee EmployeeWithRoles(params Role[] roles) => new()
    {
        Id = new EmployeeId(Guid.NewGuid()),
        CompanyId = new CompanyId(Guid.NewGuid()),
        Name = new PersonName("Ada", "Lovelace"),
        Email = new Email("ada@example.com"),
        Roles = roles,
        UserId = new UserId(Guid.NewGuid()),
        RegistrationDate = DateTimeOffset.UnixEpoch,
        IsDeleted = false,
    };

    [Fact]
    public void should_throw_when_a_locked_role_is_removed()
    {
        var employee = EmployeeWithRoles(Role.Owner, Role.Manager);

        var act = () => employee.UpdateRoles(new HashSet<Role> { Role.Manager }, new HashSet<Role> { Role.Owner },
            canManageSystemRoles: true);

        Assert.Throws<ValidationException>(act);
    }

    [Fact]
    public void should_allow_changing_other_roles_while_keeping_locked_roles()
    {
        var employee = EmployeeWithRoles(Role.Owner, Role.Staff);

        employee.UpdateRoles(new HashSet<Role> { Role.Owner, Role.Manager }, new HashSet<Role> { Role.Owner },
            canManageSystemRoles: false);

        Assert.Equivalent(new[] { Role.Owner, Role.Manager }, employee.Roles, strict: true);
        Assert.Contains(employee.Events, @event => @event is EmployeeRolesUpdated);
    }

    [Fact]
    public void should_allow_dropping_a_system_role_when_actor_manages_owners()
    {
        var employee = EmployeeWithRoles(Role.Owner, Role.Staff);

        employee.UpdateRoles(new HashSet<Role> { Role.Staff }, new HashSet<Role>(), canManageSystemRoles: true);

        Assert.Equivalent(new[] { Role.Staff }, employee.Roles, strict: true);
    }

    [Fact]
    public void should_throw_when_no_roles_provided()
    {
        var employee = EmployeeWithRoles(Role.Staff);

        var act = () => employee.UpdateRoles(new HashSet<Role>(), new HashSet<Role> { Role.Owner },
            canManageSystemRoles: false);

        Assert.Throws<ValidationException>(act);
    }

    [Fact]
    public void should_throw_when_a_system_role_is_assigned_by_actor_without_manage_owners()
    {
        var employee = EmployeeWithRoles(Role.Manager);

        var act = () => employee.UpdateRoles(new HashSet<Role> { Role.Owner, Role.Manager }, new HashSet<Role>(),
            canManageSystemRoles: false);

        Assert.Throws<ValidationException>(act);
    }

    [Fact]
    public void should_allow_assigning_a_system_role_when_actor_manages_owners()
    {
        var employee = EmployeeWithRoles(Role.Manager);

        employee.UpdateRoles(new HashSet<Role> { Role.Owner, Role.Manager }, new HashSet<Role>(),
            canManageSystemRoles: true);

        Assert.Equivalent(new[] { Role.Owner, Role.Manager }, employee.Roles, strict: true);
        Assert.Contains(employee.Events, @event => @event is EmployeeRolesUpdated);
    }

    [Fact]
    public void should_throw_when_a_system_role_is_removed_by_actor_without_manage_owners()
    {
        var employee = EmployeeWithRoles(Role.Owner, Role.Staff);

        var act = () => employee.UpdateRoles(new HashSet<Role> { Role.Staff }, new HashSet<Role>(),
            canManageSystemRoles: false);

        Assert.Throws<ValidationException>(act);
    }

    [Fact]
    public void should_not_change_roles_or_raise_events_when_a_system_role_is_rejected()
    {
        var employee = EmployeeWithRoles(Role.Manager);

        try
        {
            employee.UpdateRoles(new HashSet<Role> { Role.Owner }, new HashSet<Role>(), canManageSystemRoles: false);
        }
        catch (ValidationException)
        {
        }

        Assert.Equivalent(new[] { Role.Manager }, employee.Roles, strict: true);
        Assert.DoesNotContain(employee.Events, @event => @event is EmployeeRolesUpdated);
    }

    [Fact]
    public void should_allow_assigning_custom_roles()
    {
        var employee = EmployeeWithRoles(Role.Staff);

        employee.UpdateRoles(new HashSet<Role> { Role.Staff, new Role("Groomer") }, new HashSet<Role>(),
            canManageSystemRoles: false);

        Assert.Equivalent(new[] { Role.Staff, new Role("Groomer") }, employee.Roles, strict: true);
    }
}
