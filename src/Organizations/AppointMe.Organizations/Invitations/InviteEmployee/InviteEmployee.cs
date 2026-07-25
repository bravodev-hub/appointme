using AppointMe.Organizations.Contracts.Invitations.Events;
using AppointMe.Shared.Users;

namespace AppointMe.Organizations.Invitations.InviteEmployee;

public static class InviteEmployee
{
    extension(EmployeeInvitation)
    {
        public static EmployeeInvitation Create(CompanyId companyId, Email email, IEnumerable<Role> roles,
            UserId invitedBy, DateTimeOffset now, bool canManageSystemRoles)
        {
            var distinctRoles = roles.Distinct().ToList();
            if (distinctRoles.Count == 0)
            {
                throw new ValidationException("At least one role is required.");
            }

            if (!canManageSystemRoles)
            {
                var systemRoles = distinctRoles.Where(role => role is SystemRole).ToArray();
                if (systemRoles.Length > 0)
                {
                    throw new ValidationException(
                        $"The {string.Join(", ", systemRoles.Select(role => role.Name))} role can only be assigned by an owner.",
                        code: "system_role_not_assignable");
                }
            }

            var invitation = new EmployeeInvitation
            {
                Id = new EmployeeInvitationId(NewId()),
                CompanyId = companyId,
                Email = email,
                Roles = distinctRoles,
                Status = InvitationStatus.Pending,
                ExpiresAt = now.AddDays(7),
                InvitedBy = invitedBy,
                InvitedAt = now,
            };
            invitation.Raise(new EmployeeInvited(invitation.Id.Value, companyId.Value, email.Value));
            return invitation;
        }
    }
}
