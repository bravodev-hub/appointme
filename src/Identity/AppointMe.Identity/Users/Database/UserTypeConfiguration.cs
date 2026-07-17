using AppointMe.Shared.Users;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointMe.Identity.Users.Database;

public sealed class UserTypeConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(user => user.IdentityProviderUserId)
            .HasConversion(
                identityProviderUserId => identityProviderUserId.Value,
                value => new IdentityProviderUserId(value))
            .HasMaxLength(IdentityProviderUserId.MaxLength)
            .IsRequired();

        builder.HasIndex(user => user.IdentityProviderUserId)
            .IsUnique();

        builder.OwnsOne(user => user.Name, navigationBuilder =>
        {
            navigationBuilder.Property(personName => personName.FirstName)
                .HasColumnName("FirstName")
                .HasMaxLength(PersonName.FirstNameMaxLength)
                .IsRequired();

            navigationBuilder.Property(personName => personName.LastName)
                .HasColumnName("LastName")
                .HasMaxLength(PersonName.LastNameMaxLength);
        });

        builder.Property(user => user.Email)
            .HasConversion(email => email.Value, value => new Email(value))
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        builder.Property(user => user.RegisteredAt)
            .IsRequired();

        builder.Property(user => user.IsDeleted)
            .IsRequired();

        builder.Property<byte[]>("Version")
            .IsRowVersion()
            .IsRequired();
    }
}
