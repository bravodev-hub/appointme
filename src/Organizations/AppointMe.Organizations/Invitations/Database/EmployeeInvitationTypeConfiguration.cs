using AppointMe.Organizations.Companies;
using AppointMe.Shared.Users;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointMe.Organizations.Invitations.Database;

public sealed class EmployeeInvitationTypeConfiguration : IEntityTypeConfiguration<EmployeeInvitation>
{
    public void Configure(EntityTypeBuilder<EmployeeInvitation> builder)
    {
        builder.ToTable("EmployeeInvitations");

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Id)
            .HasConversion(id => id.Value, value => new EmployeeInvitationId(value))
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(invitation => invitation.CompanyId)
            .HasConversion(companyId => companyId.Value, value => new CompanyId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(invitation => invitation.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(invitation => invitation.Email)
            .HasConversion(email => email.Value, value => new Email(value))
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        builder.PrimitiveCollection(invitation => invitation.Roles)
            .ElementType()
            .HasConversion<RoleValueConverter>();

        builder.Property(invitation => invitation.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(invitation => invitation.ExpiresAt)
            .IsRequired();

        builder.Property(invitation => invitation.InvitedBy)
            .HasConversion(userId => userId.Value, value => new UserId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(invitation => invitation.InvitedAt)
            .IsRequired();

        builder.Property(invitation => invitation.AcceptedAt);

        builder.HasIndex(invitation => new { invitation.CompanyId, invitation.Email })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");

        builder.Property<byte[]>("Version")
            .IsRowVersion()
            .IsRequired();
    }
}
