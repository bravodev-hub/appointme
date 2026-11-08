using AppointMe.Organizations.Contracts.Settings.Permissions.Events;

namespace AppointMe.Organizations.Settings.Permissions.Infrastructure;

public sealed class CompanyPermissionsChangedEventHandler(RolePermissionOverridesCache cache)
{
    public ValueTask Handle(CompanyPermissionsChanged @event, CancellationToken cancellationToken)
    {
        return cache.InvalidateAsync(new CompanyId(@event.CompanyId), cancellationToken);
    }
}
