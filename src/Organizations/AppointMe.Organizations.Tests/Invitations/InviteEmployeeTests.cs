using AppointMe.Organizations.Invitations;
using AppointMe.Organizations.Invitations.InviteEmployee;
using AppointMe.Shared.Domain.Common;
using AppointMe.Shared.Domain.Errors;
using AppointMe.Shared.Users;

namespace AppointMe.Organizations.Tests.Invitations;

public class InviteEmployeeTests
{
    private static EmployeeInvitation CreateInvitation(Role[] roles, bool canManageSystemRoles) =>
        EmployeeInvitation.Create(
            companyId: new CompanyId(Guid.NewGuid()),
            email: new Email("invitee@example.com"),
            roles: roles,
            invitedBy: new UserId(Guid.NewGuid()),
            now: DateTimeOffset.UnixEpoch,
            canManageSystemRoles: canManageSystemRoles);

    [Fact]
    public void should_throw_when_inviting_with_a_system_role_without_manage_owners()
    {
        var act = () => CreateInvitation([Role.Owner, Role.Manager], canManageSystemRoles: false);

        Assert.Throws<ValidationException>(act);
    }

    [Fact]
    public void should_allow_inviting_an_owner_when_actor_manages_owners()
    {
        var invitation = CreateInvitation([Role.Owner], canManageSystemRoles: true);

        Assert.Equivalent(new[] { Role.Owner }, invitation.Roles, strict: true);
    }

    [Fact]
    public void should_allow_inviting_with_configurable_roles_without_manage_owners()
    {
        var invitation = CreateInvitation([Role.Manager, Role.Staff], canManageSystemRoles: false);

        Assert.Equivalent(new[] { Role.Manager, Role.Staff }, invitation.Roles, strict: true);
    }
}
