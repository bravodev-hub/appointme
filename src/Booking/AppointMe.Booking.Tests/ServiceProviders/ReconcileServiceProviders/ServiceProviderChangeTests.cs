using AppointMe.Booking.ServiceProviders.ReconcileServiceProviders;
using AppointMe.Organizations.Contracts.Employees;
using AppointMe.Shared.Authorization.Roles;

namespace AppointMe.Booking.Tests.ServiceProviders.ReconcileServiceProviders;

public class ServiceProviderChangeTests
{
    [Fact]
    public void should_create_when_staff_employee_has_no_provider()
    {
        var snapshot = Snapshot(Role.Staff);

        var change = ServiceProviderChange.Decide(existing: null, snapshot);

        Assert.Equal(new ProviderCreation(snapshot), change.Value);
    }

    [Fact]
    public void should_update_when_staff_employee_has_a_provider()
    {
        var existing = Create.ServiceProvider.Build();
        var snapshot = Snapshot(Role.Owner, Role.Staff);

        var change = ServiceProviderChange.Decide(existing, snapshot);

        Assert.Equal(new ProviderUpdate(existing, snapshot), change.Value);
    }

    [Fact]
    public void should_update_when_staff_employee_has_a_deleted_provider()
    {
        var existing = Create.ServiceProvider.Build() with { IsDeleted = true };
        var snapshot = Snapshot(Role.Staff);

        var change = ServiceProviderChange.Decide(existing, snapshot);

        Assert.Equal(new ProviderUpdate(existing, snapshot), change.Value);
    }

    [Fact]
    public void should_delete_when_employee_is_no_longer_staff()
    {
        var existing = Create.ServiceProvider.Build();

        var change = ServiceProviderChange.Decide(existing, Snapshot(Role.Owner));

        Assert.Equal(new ProviderDeletion(existing), change.Value);
    }

    [Fact]
    public void should_delete_when_employee_no_longer_exists()
    {
        var existing = Create.ServiceProvider.Build();

        var change = ServiceProviderChange.Decide(existing, snapshot: null);

        Assert.Equal(new ProviderDeletion(existing), change.Value);
    }

    [Fact]
    public void should_change_nothing_when_provider_is_already_deleted()
    {
        var existing = Create.ServiceProvider.Build() with { IsDeleted = true };

        var change = ServiceProviderChange.Decide(existing, snapshot: null);

        Assert.IsType<NoProviderChange>(change.Value);
    }

    [Fact]
    public void should_change_nothing_when_non_staff_employee_has_no_provider()
    {
        var change = ServiceProviderChange.Decide(existing: null, Snapshot(Role.Owner));

        Assert.IsType<NoProviderChange>(change.Value);
    }

    [Fact]
    public void should_change_nothing_when_neither_employee_nor_provider_exists()
    {
        var change = ServiceProviderChange.Decide(existing: null, snapshot: null);

        Assert.IsType<NoProviderChange>(change.Value);
    }

    private static EmployeeSnapshot Snapshot(params Role[] roles) =>
        new(NewId(), new CompanyId(NewId()), "Alex", "Stone", roles);
}
