using AppointMe.Organizations.Contracts.Employees.Events;

namespace AppointMe.Organizations.Employees.UpdateEmployeeRoles;

public static class UpdateEmployeeRoles
{
    extension(Employee employee)
    {
        public void UpdateRoles(IReadOnlySet<Role> roles, IReadOnlySet<Role> lockedRoles, bool canManageSystemRoles)
        {
            if (roles.Count == 0)
            {
                throw new ValidationException("At least one role is required.");
            }

            var removed = lockedRoles.Where(role => !roles.Contains(role)).ToArray();
            if (removed.Length > 0)
            {
                throw new ValidationException(
                    $"The {string.Join(", ", removed.Select(role => role.Name))} role cannot be removed from this member.");
            }

            if (!canManageSystemRoles)
            {
                var changedSystemRoles = roles.Concat(employee.Roles)
                    .Where(role => role is SystemRole)
                    .Where(role => roles.Contains(role) != employee.Roles.Contains(role))
                    .ToArray();
                if (changedSystemRoles.Length > 0)
                {
                    throw new ValidationException(
                        $"The {string.Join(", ", changedSystemRoles.Select(role => role.Name))} role can only be assigned or removed by an owner.",
                        code: "system_role_not_assignable");
                }
            }

            employee.Roles = roles.ToList();
            employee.Raise(new EmployeeRolesUpdated(
                employee.Id.Value,
                employee.CompanyId.Value,
                employee.Name.FirstName,
                employee.Name.LastName,
                [.. employee.Roles]));
        }
    }
}
