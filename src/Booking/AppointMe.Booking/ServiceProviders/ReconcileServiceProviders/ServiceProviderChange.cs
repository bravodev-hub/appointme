using AppointMe.Organizations.Contracts.Employees;
using AppointMe.Shared.Authorization.Roles;

namespace AppointMe.Booking.ServiceProviders.ReconcileServiceProviders;

public sealed record ProviderCreation(EmployeeSnapshot Snapshot);

public sealed record ProviderUpdate(ServiceProvider Existing, EmployeeSnapshot Snapshot);

public sealed record ProviderDeletion(ServiceProvider Existing);

public sealed record NoProviderChange;

/// <summary>
/// What has to happen to the local service provider so that it matches the employee it mirrors.
/// </summary>
public union ServiceProviderChange(ProviderCreation, ProviderUpdate, ProviderDeletion, NoProviderChange)
{
    /// <param name="existing">The local service provider, including a soft-deleted one; null when there is none.</param>
    /// <param name="snapshot">The employee's current state; null when the employee no longer exists.</param>
    public static ServiceProviderChange Decide(ServiceProvider? existing, EmployeeSnapshot? snapshot)
    {
        var isStaff = snapshot?.Roles.Contains(Role.Staff) == true;

        return (existing, snapshot, isStaff) switch
        {
            // Doesn't exist, needs to be created
            (existing: null, snapshot: not null, isStaff: true) => new ProviderCreation(snapshot),

            // Exists, needs update (and restoration if it was deleted)
            (existing: not null, snapshot: not null, isStaff: true) => new ProviderUpdate(existing, snapshot),

            // Exists but the employee is gone or no longer staff, needs deletion
            (existing: { IsDeleted: false }, snapshot: _, isStaff: false) => new ProviderDeletion(existing),

            // Already deleted, or never existed and shouldn't
            _ => new NoProviderChange()
        };
    }
}
