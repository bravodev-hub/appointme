using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointMe.Booking.ServiceProviders.Database;

public sealed class ServiceProviderTypeConfiguration : IEntityTypeConfiguration<ServiceProvider>
{
    public void Configure(EntityTypeBuilder<ServiceProvider> builder)
    {
        builder.ToTable("ServiceProviders");

        builder.HasKey(serviceProvider => serviceProvider.Id);

        builder.Property(serviceProvider => serviceProvider.Id)
            .HasConversion(id => id.Value, value => new ServiceProviderId(value))
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        builder.Property(serviceProvider => serviceProvider.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.OwnsOne(serviceProvider => serviceProvider.Name, name =>
        {
            name.Property(personName => personName.FirstName)
                .HasColumnName("FirstName")
                .HasMaxLength(PersonName.FirstNameMaxLength)
                .IsRequired();

            name.Property(personName => personName.LastName)
                .HasColumnName("LastName")
                .HasMaxLength(PersonName.LastNameMaxLength);
        });

        builder.Property(serviceProvider => serviceProvider.IsDeleted)
            .IsRequired();

        builder.Property<byte[]>("Version")
            .IsRowVersion()
            .IsRequired();
    }
}
